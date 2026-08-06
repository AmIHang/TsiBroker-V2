using TsiBroker.Core.Messaging;
using TsiBroker.Core.RailwayUndertakings;

namespace TsiBroker.ApiService.Queues;

public record QueueStatusResponse(string QueueName, string RailwayUndertakingName, bool IsActive, int? MessageCount);

// Each active RU owns one queue (see RailwayUndertakingConsumerCoordinator) — this lists all
// of them together with whether their consume loop is currently running and how many
// messages are currently waiting in the queue.
public static class QueueEndpoints
{
    public static void MapQueueEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/queues").RequireAuthorization();

        group.MapGet("/", async (RailwayUndertakingStore store, IMessageConsumer consumer, CancellationToken cancellationToken) =>
        {
            var railwayUndertakings = await store.GetAllAsync();
            var queues = await Task.WhenAll(railwayUndertakings
                .OrderBy(ru => ru.Name, StringComparer.OrdinalIgnoreCase)
                .Select(async ru => new QueueStatusResponse(
                    RabbitMqQueueNaming.ForPartition(ru.Name),
                    ru.Name,
                    consumer.IsPartitionActive(ru.Name),
                    await consumer.GetMessageCountAsync(ru.Name, cancellationToken))));

            return Results.Ok(queues);
        });
    }
}
