using Microsoft.Extensions.Logging;

namespace TsiBroker.Core.Messaging;

public class DebugMessageConsumer(ILogger<DebugMessageConsumer> logger) : IMessageConsumer
{
    public async Task RunAsync(
        Func<BrokerMessage, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Debug message consumer active — no real queue is being read");

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected on shutdown.
        }
    }
}
