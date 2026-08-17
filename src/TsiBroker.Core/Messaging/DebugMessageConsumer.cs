using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace TsiBroker.Core.Messaging;

public class DebugMessageConsumer(ILogger<DebugMessageConsumer> logger) : IMessageConsumer
{
    private readonly ConcurrentDictionary<string, byte> _startedPartitions = new();

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

    public Task StartPartitionAsync(
        string partitionKey,
        Func<BrokerMessage, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        if (_startedPartitions.TryAdd(partitionKey, 0))
        {
            logger.LogInformation("Debug message consumer: would start consuming partition '{PartitionKey}'", partitionKey);
        }

        return Task.CompletedTask;
    }

    public Task StopPartitionAsync(string partitionKey, CancellationToken cancellationToken = default)
    {
        if (_startedPartitions.TryRemove(partitionKey, out _))
        {
            logger.LogInformation("Debug message consumer: would stop consuming partition '{PartitionKey}'", partitionKey);
        }

        return Task.CompletedTask;
    }

    public bool IsPartitionActive(string partitionKey) => _startedPartitions.ContainsKey(partitionKey);

    public Task<int?> GetMessageCountAsync(string partitionKey, CancellationToken cancellationToken = default) =>
        Task.FromResult<int?>(_startedPartitions.ContainsKey(partitionKey) ? 0 : null);

    public Task<int?> GetDeadLetterMessageCountAsync(string partitionKey, CancellationToken cancellationToken = default) =>
        Task.FromResult<int?>(_startedPartitions.ContainsKey(partitionKey) ? 0 : null);
}
