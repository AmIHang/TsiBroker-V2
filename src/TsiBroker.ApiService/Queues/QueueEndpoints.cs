using TsiBroker.ApiService.RailwayUndertakings;
using TsiBroker.Core.InfrastructureOperators;
using TsiBroker.Core.Messaging;
using TsiBroker.Core.RailwayUndertakings;

namespace TsiBroker.ApiService.Queues;

public record QueueStatusResponse(
    string QueueName,
    Guid InfrastructureOperatorId,
    string InfrastructureOperatorName,
    bool IsActive,
    bool IsPaused,
    string? PauseReason,
    int? MessageCount,
    int? ErrorMessageCount);

public record EvuQueueStatusResponse(
    string QueueName,
    Guid RailwayUndertakingId,
    string RailwayUndertakingName,
    bool IsActive,
    bool IsPaused,
    string? PauseReason,
    int? MessageCount,
    int? ErrorMessageCount);

// Each active Infrastrukturbetreiber owns one outbound queue (see
// InfrastructureOperatorConsumerCoordinator) — this lists all of them together with whether
// their consume loop is currently running or paused (IM system unreachable), and how many
// messages are currently waiting in the queue vs. its error queue.
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
                    io.Id,
                    io.Name,
                    consumer.IsPartitionActive(io.Name),
                    io.IsQueuePaused,
                    io.PauseReason,
                    await consumer.GetMessageCountAsync(io.Name, cancellationToken),
                    await consumer.GetDeadLetterMessageCountAsync(io.Name, cancellationToken))));

            return Results.Ok(queues);
        });

        // The broker -> EVU counterpart of "/" above — one outbound queue per EVU (see
        // EvuDeliveryCoordinator), including whether it's currently paused (customer system
        // unreachable) and how many messages are sitting in its error queue.
        group.MapGet("/evu", async (RailwayUndertakingStore store, IMessageConsumer consumer, CancellationToken cancellationToken) =>
        {
            var railwayUndertakings = await store.GetAllAsync();
            var queues = await Task.WhenAll(railwayUndertakings
                .OrderBy(ru => ru.Name, StringComparer.OrdinalIgnoreCase)
                .Select(async ru =>
                {
                    var partitionKey = EvuDeliveryCoordinator.PartitionKeyFor(ru);
                    return new EvuQueueStatusResponse(
                        RabbitMqQueueNaming.ForPartition(partitionKey),
                        ru.Id,
                        ru.Name,
                        ru.IsActive,
                        ru.IsQueuePaused,
                        ru.PauseReason,
                        await consumer.GetMessageCountAsync(partitionKey, cancellationToken),
                        await consumer.GetDeadLetterMessageCountAsync(partitionKey, cancellationToken));
                }));

            return Results.Ok(queues);
        });
    }
}
