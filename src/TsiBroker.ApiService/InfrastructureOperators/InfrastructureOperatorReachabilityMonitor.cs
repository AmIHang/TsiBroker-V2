using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TsiBroker.Core.InfrastructureOperators;

namespace TsiBroker.ApiService.InfrastructureOperators;

// Polls a paused Infrastrukturbetreiber's /heartbeat SOAP endpoint on a backoff schedule until
// it's reachable again, then clears the pause and restarts its outbound queue consumption. Also
// notices — within ExternalResumeCheckInterval — if the pause was already cleared some other way
// (a manual resume via InfrastructureOperatorEndpoints) and reacts to that instead of waiting out
// the rest of the current interval. Mirrors EvuReachabilityMonitor for the opposite direction.
public class InfrastructureOperatorReachabilityMonitor(
    InfrastructureOperatorStore infrastructureOperatorStore,
    IsbApiClient isbApiClient,
    InfrastructureOperatorConsumerCoordinator coordinator,
    ILogger<InfrastructureOperatorReachabilityMonitor> logger)
{
    // The last (60 min) interval repeats indefinitely once the schedule is exhausted.
    private static readonly TimeSpan[] BackoffSchedule =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(60),
        TimeSpan.FromMinutes(60),
        TimeSpan.FromMinutes(60),
    ];

    private static readonly TimeSpan ExternalResumeCheckInterval = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _pollers = new();

    // No-op if already polling this operator (idempotent, matches IMessageConsumer.StartPartitionAsync's
    // own idempotent-start convention).
    public void StartPolling(InfrastructureOperator infrastructureOperator)
    {
        var cts = new CancellationTokenSource();
        if (!_pollers.TryAdd(infrastructureOperator.Id, cts))
        {
            cts.Dispose();
            return;
        }

        _ = PollAsync(infrastructureOperator.Id, infrastructureOperator.PauseBackoffStep, cts.Token);
    }

    // Only signals cancellation — PollAsync's own finally block owns removing/disposing its
    // CancellationTokenSource, so a cancelled Task.Delay can never race a concurrent Dispose.
    public void CancelPolling(Guid infrastructureOperatorId)
    {
        if (_pollers.TryGetValue(infrastructureOperatorId, out var cts))
        {
            cts.Cancel();
        }
    }

    private async Task PollAsync(Guid infrastructureOperatorId, int startingBackoffStep, CancellationToken cancellationToken)
    {
        try
        {
            var backoffStep = startingBackoffStep;
            while (true)
            {
                var interval = BackoffSchedule[Math.Min(backoffStep, BackoffSchedule.Length - 1)];
                var resumedExternally = !await WaitOutIntervalAsync(infrastructureOperatorId, interval, cancellationToken);
                if (resumedExternally)
                {
                    return;
                }

                var infrastructureOperator = await infrastructureOperatorStore.FindByIdAsync(infrastructureOperatorId);
                if (infrastructureOperator is null || !infrastructureOperator.IsQueuePaused)
                {
                    return;
                }

                if (await isbApiClient.CheckHeartbeatAsync(infrastructureOperator, cancellationToken))
                {
                    logger.LogInformation(
                        "IM {InfrastructureOperatorName} reachable again — resuming queue processing",
                        infrastructureOperator.Name);
                    await infrastructureOperatorStore.SetQueuePauseStateAsync(infrastructureOperator.Id, isPaused: false, reason: null, backoffStep: 0);
                    await coordinator.StartAsync(infrastructureOperator);
                    return;
                }

                backoffStep++;
                await infrastructureOperatorStore.SetQueuePauseStateAsync(infrastructureOperator.Id, isPaused: true, reason: "Unreachable", backoffStep: backoffStep);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on manual resume (CancelPolling) or shutdown.
        }
        finally
        {
            if (_pollers.TryRemove(infrastructureOperatorId, out var cts))
            {
                cts.Dispose();
            }
        }
    }

    // Waits out interval in short slices, checking after each one whether the pause was cleared
    // externally. Returns true if the full interval elapsed, false if it resumed early (in which
    // case the partition has already been (re)started before returning).
    private async Task<bool> WaitOutIntervalAsync(Guid infrastructureOperatorId, TimeSpan interval, CancellationToken cancellationToken)
    {
        var remaining = interval;
        while (remaining > TimeSpan.Zero)
        {
            var step = remaining < ExternalResumeCheckInterval ? remaining : ExternalResumeCheckInterval;
            await Task.Delay(step, cancellationToken);
            remaining -= step;

            var infrastructureOperator = await infrastructureOperatorStore.FindByIdAsync(infrastructureOperatorId);
            if (infrastructureOperator is null)
            {
                return false;
            }

            if (!infrastructureOperator.IsQueuePaused)
            {
                logger.LogInformation(
                    "IM {InfrastructureOperatorName} was resumed externally — resuming queue processing",
                    infrastructureOperator.Name);
                await coordinator.StartAsync(infrastructureOperator);
                return false;
            }
        }

        return true;
    }
}
