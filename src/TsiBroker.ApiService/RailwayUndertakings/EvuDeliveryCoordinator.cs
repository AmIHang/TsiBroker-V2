using Microsoft.Extensions.Logging;
using TsiBroker.Core.Messaging;
using TsiBroker.Core.RailwayUndertakings;

namespace TsiBroker.ApiService.RailwayUndertakings;

// Keeps each active, non-paused EVU's outbound queue consumption in sync with
// RailwayUndertakingStore — started as soon as an EVU is created/activated/resumed, stopped as
// soon as it's deactivated/deleted/paused, and reconciled once at process startup. Mirrors
// InfrastructureOperatorConsumerCoordinator's start/stop wiring, but for the opposite
// direction: broker -> EVU delivery, with real logic instead of a placeholder (see
// HandleMessageAsync).
public class EvuDeliveryCoordinator(
    IMessageConsumer consumer,
    IMessagePublisher publisher,
    EvuApiClient evuApiClient,
    EvuMessageAuthorizationService authorizationService,
    RailwayUndertakingStore railwayUndertakingStore,
    Lazy<EvuReachabilityMonitor> reachabilityMonitor,
    ILogger<EvuDeliveryCoordinator> logger)
{
    // On the first delivery failure, the customer's reachability decides what happens next: if
    // reachable, up to 2 further attempts (1 minute apart); if not, processing pauses instead
    // of retrying (see EvuReachabilityMonitor).
    private const int MaxAttempts = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

    public async Task StartAllActiveAsync(IEnumerable<RailwayUndertaking> railwayUndertakings)
    {
        foreach (var railwayUndertaking in railwayUndertakings)
        {
            await StartAsync(railwayUndertaking);
        }
    }

    public Task StartAsync(RailwayUndertaking railwayUndertaking) =>
        railwayUndertaking.IsActive && !railwayUndertaking.IsQueuePaused
            ? consumer.StartPartitionAsync(
                PartitionKeyFor(railwayUndertaking),
                (message, cancellationToken) => HandleMessageAsync(railwayUndertaking.Id, message, cancellationToken))
            : Task.CompletedTask;

    public Task StopAsync(RailwayUndertaking railwayUndertaking) => StopAsync(railwayUndertaking.Name);

    public Task StopAsync(string railwayUndertakingName) =>
        consumer.StopPartitionAsync(PartitionKeyFor(railwayUndertakingName));

    // Prefixed to keep this direction's partitions distinct from the ISB-keyed partitions
    // InfrastructureOperatorConsumerCoordinator uses for the opposite (EVU -> broker) direction.
    public static string PartitionKeyFor(RailwayUndertaking railwayUndertaking) => PartitionKeyFor(railwayUndertaking.Name);

    public static string PartitionKeyFor(string railwayUndertakingName) => $"evu:{railwayUndertakingName}";

    private async Task HandleMessageAsync(Guid railwayUndertakingId, BrokerMessage message, CancellationToken cancellationToken)
    {
        // Reloaded fresh (rather than captured in the StartAsync closure) so an admin edit to
        // SystemUrl/ApiKeyBrokerToEvu/ISB assignments takes effect on the very next message,
        // without needing to restart the partition.
        var railwayUndertaking = await railwayUndertakingStore.FindByIdAsync(railwayUndertakingId);
        if (railwayUndertaking is null)
        {
            return;
        }

        var partitionKey = PartitionKeyFor(railwayUndertaking);

        var authorization = await authorizationService.AuthorizeAsync(railwayUndertaking, message);
        if (!authorization.IsSuccess)
        {
            logger.LogWarning(
                "Rejecting message {MessageId} from {Sender} to EVU {RailwayUndertakingName}: {Reason} — moving to error queue",
                message.Id,
                message.Sender,
                railwayUndertaking.Name,
                authorization.FailureReason);
            await publisher.PublishToDeadLetterAsync(partitionKey, message, cancellationToken);
            return;
        }

        // A rejection (EVU processed the message and responded with a non-2xx status) is treated
        // very differently from a transport failure (no response at all): it's a per-message,
        // application-level failure, not a sign the EVU is down, so it goes straight to the error
        // queue below — no retry, no reachability check, and critically, no queue pause. Only a
        // TransportFailure ever reaches the reachability check/pause logic further down.
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            if (attempt > 1)
            {
                await Task.Delay(RetryDelay, cancellationToken);
            }

            var outcome = await evuApiClient.DeliverMessageAsync(railwayUndertaking, message, cancellationToken);
            if (outcome == EvuDeliveryOutcome.Delivered)
            {
                if (railwayUndertaking.PauseBackoffStep != 0)
                {
                    await railwayUndertakingStore.SetQueuePauseStateAsync(railwayUndertaking.Id, false, null, 0);
                }

                return;
            }

            if (outcome == EvuDeliveryOutcome.Rejected)
            {
                logger.LogError(
                    "EVU {RailwayUndertakingName} rejected message {MessageId} — moving to error queue",
                    railwayUndertaking.Name,
                    message.Id);
                await publisher.PublishToDeadLetterAsync(partitionKey, message, cancellationToken);
                return;
            }

            if (attempt == 1)
            {
                // First failure: check reachability before spending further attempts — an
                // unreachable customer pauses processing instead of being retried.
                var reachable = await evuApiClient.CheckHealthAsync(railwayUndertaking, cancellationToken);
                if (!reachable)
                {
                    logger.LogWarning(
                        "EVU {RailwayUndertakingName} is unreachable — pausing queue processing",
                        railwayUndertaking.Name);
                    await railwayUndertakingStore.SetQueuePauseStateAsync(
                        railwayUndertaking.Id, isPaused: true, reason: "Unreachable", backoffStep: 0);
                    reachabilityMonitor.Value.StartPolling(railwayUndertaking);

                    // Stopping this partition from within its own handler would race with
                    // ProcessDeliveryAsync trying to ack/nack on the very channel it closes, so
                    // it's scheduled to run after this delivery is done being handled — see
                    // MessagePausedException's doc comment for why the message ends up requeued
                    // regardless.
                    _ = Task.Run(() => StopAsync(railwayUndertaking));
                    throw new MessagePausedException();
                }
            }
        }

        logger.LogError(
            "Giving up delivering message {MessageId} to EVU {RailwayUndertakingName} after {Attempts} attempts — moving to error queue",
            message.Id,
            railwayUndertaking.Name,
            MaxAttempts);
        await publisher.PublishToDeadLetterAsync(partitionKey, message, cancellationToken);
    }
}
