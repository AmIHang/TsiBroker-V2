using TsiBroker.Core.InfrastructureOperators;
using TsiBroker.Core.Messaging;

namespace TsiBroker.ApiService.Queues;

public record QueueStatusResponse(string QueueName, string InfrastructureOperatorName, bool IsActive, int? MessageCount);

// Each active Infrastrukturbetreiber owns one outbound queue (see
// InfrastructureOperatorConsumerCoordinator) — this lists all of them together with whether
// their consume loop is currently running and how many messages are currently waiting in the
// queue.
public static class QueueEndpoints
{
    public static void MapQueueEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/queues").RequireAuthorization();

        group.MapGet("/", async (InfrastructureOperatorStore store, IMessageConsumer consumer, CancellationToken cancellationToken) =>
        {
            var infrastructureOperators = await store.GetAllAsync();
            var queues = await Task.WhenAll(infrastructureOperators
                .OrderBy(io => io.Name, StringComparer.OrdinalIgnoreCase)
                .Select(async io => new QueueStatusResponse(
                    RabbitMqQueueNaming.ForPartition(io.Name),
                    io.Name,
                    consumer.IsPartitionActive(io.Name),
                    await consumer.GetMessageCountAsync(io.Name, cancellationToken))));

            return Results.Ok(queues);
        });
    }
}
