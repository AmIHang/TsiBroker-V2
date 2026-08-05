namespace TsiBroker.Core.Messaging;

public interface IMessageConsumer
{
    // Consumes messages one at a time, strictly in the order they were queued, invoking
    // handler for each. Implementations must not start processing the next message until
    // the current one has been handled (successfully, or given up on after retrying) —
    // this is what keeps e.g. two messages for the same RU/IM in the order they arrived.
    // Runs until cancellationToken is cancelled.
    Task RunAsync(
        Func<BrokerMessage, CancellationToken, Task> handler,
        CancellationToken cancellationToken);
}
