namespace TsiBroker.Core.Messaging;

public interface IMessageConsumer
{
    // Consumes the single default queue, strictly in order, invoking handler for each
    // message. Implementations must not start processing the next message until the
    // current one has been handled (successfully, or given up on after retrying).
    // Runs until cancellationToken is cancelled.
    Task RunAsync(
        Func<BrokerMessage, CancellationToken, Task> handler,
        CancellationToken cancellationToken);

    // Starts an independent, strictly-ordered consume loop for partitionKey (e.g. one RU),
    // running in the background until StopPartitionAsync(partitionKey) is called or this
    // consumer is disposed — it does NOT stop when cancellationToken (which only bounds the
    // startup itself: opening a channel, declaring the queue) is cancelled. A stuck/retrying
    // message in one partition never delays another partition's messages. Calling this again
    // for a partition that's already running is a no-op.
    Task StartPartitionAsync(
        string partitionKey,
        Func<BrokerMessage, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default);

    // Stops the consume loop for partitionKey, if one is running. A no-op if it isn't.
    Task StopPartitionAsync(string partitionKey, CancellationToken cancellationToken = default);
}
