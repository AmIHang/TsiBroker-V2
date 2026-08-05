using Microsoft.Extensions.Logging;
using TsiBroker.Core.Messaging;
using TsiBroker.Core.RailwayUndertakings;

namespace TsiBroker.ApiService.RailwayUndertakings;

// Keeps each active RU's queue consumption in sync with RailwayUndertakingStore: started as
// soon as an RU is created/activated, stopped as soon as it's deactivated/deleted/renamed
// (renaming changes the queue name — see TsiMessageEndpoints.PartitionKey), and reconciled
// once at process startup for whatever is already active. The actual relay-to-RU/IM logic
// doesn't exist yet (see wiki/Business-Flow.md); HandleMessageAsync is a logging placeholder
// standing in for it, the same role DebugMessagePublisher plays for publishing.
public class RailwayUndertakingConsumerCoordinator(
    IMessageConsumer consumer,
    ILogger<RailwayUndertakingConsumerCoordinator> logger)
{
    public async Task StartAllActiveAsync(IEnumerable<RailwayUndertaking> railwayUndertakings)
    {
        foreach (var railwayUndertaking in railwayUndertakings)
        {
            await StartAsync(railwayUndertaking);
        }
    }

    public Task StartAsync(RailwayUndertaking railwayUndertaking) =>
        railwayUndertaking.IsActive
            ? consumer.StartPartitionAsync(railwayUndertaking.Name, HandleMessageAsync)
            : Task.CompletedTask;

    public Task StopAsync(string railwayUndertakingName) =>
        consumer.StopPartitionAsync(railwayUndertakingName);

    private Task HandleMessageAsync(BrokerMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Would relay message {MessageId} from {Sender} to {Receiver} (relay-to-IM not implemented yet)",
            message.Id,
            message.Sender,
            message.Receiver);

        return Task.CompletedTask;
    }
}
