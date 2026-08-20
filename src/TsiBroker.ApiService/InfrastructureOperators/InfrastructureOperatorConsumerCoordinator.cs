using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TsiBroker.Core.InfrastructureOperators;
using TsiBroker.Core.Messaging;

namespace TsiBroker.ApiService.InfrastructureOperators;

// Keeps each active, non-paused Infrastrukturbetreiber's outbound queue consumption in sync with
// InfrastructureOperatorStore — started as soon as an operator is created/activated/resumed,
// stopped as soon as it's deactivated/deleted/paused (renaming migrates the partition — see
// InfrastructureOperatorEndpoints), and reconciled once at process startup. Relays each message
// onward to the operator's own SOAP endpoint (InfrastructureOperator.SystemUrl) — see
// HandleMessageAsync; mirrors EvuDeliveryCoordinator for the opposite (broker -> EVU) direction.
public class InfrastructureOperatorConsumerCoordinator(
    IMessageConsumer consumer,
    IMessagePublisher publisher,
    IsbApiClient isbApiClient,
    IsbMessageAuthorizationService authorizationService,
    InfrastructureOperatorStore infrastructureOperatorStore,
    Lazy<InfrastructureOperatorReachabilityMonitor> reachabilityMonitor,
    IOptions<InfrastructureOperatorDeliveryOptions> deliveryOptions,
    ILogger<InfrastructureOperatorConsumerCoordinator> logger)
{
    public async Task StartAllActiveAsync(IEnumerable<InfrastructureOperator> infrastructureOperators)
    {
        foreach (var infrastructureOperator in infrastructureOperators)
        {
            await StartAsync(infrastructureOperator);
        }
    }

    public Task StartAsync(InfrastructureOperator infrastructureOperator) =>
        infrastructureOperator.IsActive && !infrastructureOperator.IsQueuePaused
            ? consumer.StartPartitionAsync(
                infrastructureOperator.Name,
                (message, cancellationToken) => HandleMessageAsync(infrastructureOperator.Id, message, cancellationToken))
            : Task.CompletedTask;

    public Task StopAsync(InfrastructureOperator infrastructureOperator) => StopAsync(infrastructureOperator.Name);

    public Task StopAsync(string infrastructureOperatorName) =>
        consumer.StopPartitionAsync(infrastructureOperatorName);

    private async Task HandleMessageAsync(Guid infrastructureOperatorId, BrokerMessage message, CancellationToken cancellationToken)
    {
        // Reloaded fresh (rather than captured in the StartAsync closure) so an admin edit to
        // SystemUrl takes effect on the very next message, without needing to restart the
        // partition.
        var infrastructureOperator = await infrastructureOperatorStore.FindByIdAsync(infrastructureOperatorId);
        if (infrastructureOperator is null)
        {
            return;
        }

        var partitionKey = infrastructureOperator.Name;

        var authorization = await authorizationService.AuthorizeAsync(infrastructureOperator, message);
        if (!authorization.IsSuccess)
        {
            logger.LogWarning(
                "Rejecting message {MessageId} from {Sender} to IM {InfrastructureOperatorName}: {Reason} — moving to error queue",
                message.Id,
                message.Sender,
                infrastructureOperator.Name,
                authorization.FailureReason);
            await publisher.PublishToDeadLetterAsync(partitionKey, message, cancellationToken);
            return;
        }

        // Spec 2.3.2 step 2 distinguishes what the response to a delivery attempt means (see
        // DeliveryOutcome) rather than treating every failure alike:
        //  - Acknowledged/NegativeAcknowledged (2a): done — an ACK and a NACK both count as a
        //    completed send, a NACK is just logged for analysis, not retried.
        //  - InvalidResponse (2b): discarded outright, no retry at all.
        //  - TimedOut/TransportFailure (2c, plus the not-spec'd "IM unreachable" case): resend,
        //    checking reachability once up front, until the message's max age is reached.
        var reachabilityChecked = false;
        while (true)
        {
            var outcome = await isbApiClient.DeliverMessageAsync(infrastructureOperator, message, cancellationToken);

            if (outcome is DeliveryOutcome.Acknowledged or DeliveryOutcome.NegativeAcknowledged)
            {
                if (infrastructureOperator.PauseBackoffStep != 0)
                {
                    await infrastructureOperatorStore.SetQueuePauseStateAsync(infrastructureOperator.Id, false, null, 0);
                }

                return;
            }

            if (outcome == DeliveryOutcome.InvalidResponse)
            {
                logger.LogError(
                    "IM {InfrastructureOperatorName} returned neither ACK nor NACK for message {MessageId} — moving to error queue",
                    infrastructureOperator.Name,
                    message.Id);
                await publisher.PublishToDeadLetterAsync(partitionKey, message, cancellationToken);
                return;
            }

            if (!reachabilityChecked)
            {
                // First failure: check reachability before spending further attempts — an
                // unreachable IM pauses processing instead of being retried.
                reachabilityChecked = true;
                var reachable = await isbApiClient.CheckHeartbeatAsync(infrastructureOperator, cancellationToken);
                if (!reachable)
                {
                    logger.LogWarning(
                        "IM {InfrastructureOperatorName} is unreachable — pausing queue processing",
                        infrastructureOperator.Name);
                    await infrastructureOperatorStore.SetQueuePauseStateAsync(
                        infrastructureOperator.Id, isPaused: true, reason: "Unreachable", backoffStep: 0);
                    reachabilityMonitor.Value.StartPolling(infrastructureOperator);

                    // Stopping this partition from within its own handler would race with
                    // ProcessDeliveryAsync trying to ack/nack on the very channel it closes, so
                    // it's scheduled to run after this delivery is done being handled — see
                    // MessagePausedException's doc comment for why the message ends up requeued
                    // regardless.
                    _ = Task.Run(() => StopAsync(infrastructureOperator));
                    throw new MessagePausedException();
                }
            }

            var age = DateTimeOffset.UtcNow - message.CreatedAt;
            if (age >= deliveryOptions.Value.MaxMessageAge)
            {
                logger.LogError(
                    "Giving up delivering message {MessageId} to IM {InfrastructureOperatorName} — exceeded max message age {MaxMessageAge} (last outcome: {Outcome}) — moving to error queue",
                    message.Id,
                    infrastructureOperator.Name,
                    deliveryOptions.Value.MaxMessageAge,
                    outcome);
                await publisher.PublishToDeadLetterAsync(partitionKey, message, cancellationToken);
                return;
            }

            await Task.Delay(deliveryOptions.Value.RetryDelay, cancellationToken);
        }
    }
}
