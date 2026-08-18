using Microsoft.Extensions.Logging;
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
    ILogger<InfrastructureOperatorConsumerCoordinator> logger)
{
    // On the first delivery failure, the IM's reachability decides what happens next: if
    // reachable, up to 2 further attempts (1 minute apart); if not, processing pauses instead
    // of retrying (see InfrastructureOperatorReachabilityMonitor).
    private const int MaxAttempts = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

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

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            if (attempt > 1)
            {
                await Task.Delay(RetryDelay, cancellationToken);
            }

            var delivered = await TryDeliverAsync(infrastructureOperator, message, cancellationToken);
            if (delivered)
            {
                if (infrastructureOperator.PauseBackoffStep != 0)
                {
                    await infrastructureOperatorStore.SetQueuePauseStateAsync(infrastructureOperator.Id, false, null, 0);
                }

                return;
            }

            if (attempt == 1)
            {
                // First failure: check reachability before spending further attempts — an
                // unreachable IM pauses processing instead of being retried.
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
        }

        logger.LogError(
            "Giving up delivering message {MessageId} to IM {InfrastructureOperatorName} after {Attempts} attempts — moving to error queue",
            message.Id,
            infrastructureOperator.Name,
            MaxAttempts);
        await publisher.PublishToDeadLetterAsync(partitionKey, message, cancellationToken);
    }

    private async Task<bool> TryDeliverAsync(InfrastructureOperator infrastructureOperator, BrokerMessage message, CancellationToken cancellationToken)
    {
        try
        {
            return await isbApiClient.DeliverMessageAsync(infrastructureOperator, message, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
