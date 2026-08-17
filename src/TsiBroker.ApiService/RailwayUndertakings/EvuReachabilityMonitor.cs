using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TsiBroker.Core.RailwayUndertakings;

namespace TsiBroker.ApiService.RailwayUndertakings;

// Polls a paused EVU's /health on a backoff schedule until it's reachable again, then clears
// the pause and restarts its outbound queue consumption. Also notices — within
// ExternalResumeCheckInterval — if the pause was already cleared some other way (a manual
// restart via RailwayUndertakingEndpoints, or an inbound whoami/message request from the
// customer itself, handled by TsiBroker.Ru.Api in a separate process — see that project's
// Messages/TsiMessageEndpoints.cs and WhoAmI/WhoAmIEndpoints.cs) and reacts to that instead of
// waiting out the rest of the current interval.
public class EvuReachabilityMonitor(
    RailwayUndertakingStore railwayUndertakingStore,
    EvuApiClient evuApiClient,
    EvuDeliveryCoordinator coordinator,
    ILogger<EvuReachabilityMonitor> logger)
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

    // No-op if already polling this EVU (idempotent, matches IMessageConsumer.StartPartitionAsync's
    // own idempotent-start convention).
    public void StartPolling(RailwayUndertaking railwayUndertaking)
    {
        var cts = new CancellationTokenSource();
        if (!_pollers.TryAdd(railwayUndertaking.Id, cts))
        {
            cts.Dispose();
            return;
        }

        _ = PollAsync(railwayUndertaking.Id, railwayUndertaking.PauseBackoffStep, cts.Token);
    }

    // Only signals cancellation — PollAsync's own finally block owns removing/disposing its
    // CancellationTokenSource, so a cancelled Task.Delay can never race a concurrent Dispose.
    public void CancelPolling(Guid railwayUndertakingId)
    {
        if (_pollers.TryGetValue(railwayUndertakingId, out var cts))
        {
            cts.Cancel();
        }
    }

    private async Task PollAsync(Guid railwayUndertakingId, int startingBackoffStep, CancellationToken cancellationToken)
    {
        try
        {
            var backoffStep = startingBackoffStep;
            while (true)
            {
                var interval = BackoffSchedule[Math.Min(backoffStep, BackoffSchedule.Length - 1)];
                var resumedExternally = !await WaitOutIntervalAsync(railwayUndertakingId, interval, cancellationToken);
                if (resumedExternally)
                {
                    return;
                }

                var railwayUndertaking = await railwayUndertakingStore.FindByIdAsync(railwayUndertakingId);
                if (railwayUndertaking is null || !railwayUndertaking.IsQueuePaused)
                {
                    return;
                }

                if (await evuApiClient.CheckHealthAsync(railwayUndertaking, cancellationToken))
                {
                    logger.LogInformation(
                        "EVU {RailwayUndertakingName} reachable again — resuming queue processing",
                        railwayUndertaking.Name);
                    await railwayUndertakingStore.SetQueuePauseStateAsync(railwayUndertaking.Id, isPaused: false, reason: null, backoffStep: 0);
                    await coordinator.StartAsync(railwayUndertaking);
                    return;
                }

                backoffStep++;
                await railwayUndertakingStore.SetQueuePauseStateAsync(railwayUndertaking.Id, isPaused: true, reason: "Unreachable", backoffStep: backoffStep);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on manual resume (CancelPolling) or shutdown.
        }
        finally
        {
            if (_pollers.TryRemove(railwayUndertakingId, out var cts))
            {
                cts.Dispose();
            }
        }
    }

    // Waits out interval in short slices, checking after each one whether the pause was cleared
    // externally. Returns true if the full interval elapsed, false if it resumed early (in which
    // case the partition has already been (re)started before returning).
    private async Task<bool> WaitOutIntervalAsync(Guid railwayUndertakingId, TimeSpan interval, CancellationToken cancellationToken)
    {
        var remaining = interval;
        while (remaining > TimeSpan.Zero)
        {
            var step = remaining < ExternalResumeCheckInterval ? remaining : ExternalResumeCheckInterval;
            await Task.Delay(step, cancellationToken);
            remaining -= step;

            var railwayUndertaking = await railwayUndertakingStore.FindByIdAsync(railwayUndertakingId);
            if (railwayUndertaking is null)
            {
                return false;
            }

            if (!railwayUndertaking.IsQueuePaused)
            {
                logger.LogInformation(
                    "EVU {RailwayUndertakingName} was resumed externally — resuming queue processing",
                    railwayUndertaking.Name);
                await coordinator.StartAsync(railwayUndertaking);
                return false;
            }
        }

        return true;
    }
}
