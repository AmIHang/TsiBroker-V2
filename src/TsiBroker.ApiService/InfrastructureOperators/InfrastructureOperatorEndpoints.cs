using TsiBroker.Core.InfrastructureOperators;
using TsiBroker.Core.RailwayUndertakings;

namespace TsiBroker.ApiService.InfrastructureOperators;

public record CreateInfrastructureOperatorRequest(string Name, string RicsCode, string SystemUrl);

public record UpdateInfrastructureOperatorRequest(string Name, string RicsCode, string SystemUrl);

public record SetInfrastructureOperatorActiveRequest(bool IsActive);

public static class InfrastructureOperatorEndpoints
{
    public static void MapInfrastructureOperatorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/infrastructure-operators").RequireAuthorization();

        group.MapGet("/", async (InfrastructureOperatorStore store) =>
            Results.Ok(await store.GetAllAsync()));

        group.MapPost("/", async (
            CreateInfrastructureOperatorRequest request,
            InfrastructureOperatorStore store,
            InfrastructureOperatorConsumerCoordinator consumerCoordinator) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name)
                || string.IsNullOrWhiteSpace(request.RicsCode)
                || string.IsNullOrWhiteSpace(request.SystemUrl))
            {
                return Results.BadRequest();
            }

            var created = await store.AddAsync(request.Name.Trim(), request.RicsCode.Trim(), request.SystemUrl.Trim());
            await consumerCoordinator.StartAsync(created);
            return Results.Created($"/api/infrastructure-operators/{created.Id}", created);
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateInfrastructureOperatorRequest request,
            InfrastructureOperatorStore store,
            InfrastructureOperatorConsumerCoordinator consumerCoordinator) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name)
                || string.IsNullOrWhiteSpace(request.RicsCode)
                || string.IsNullOrWhiteSpace(request.SystemUrl))
            {
                return Results.BadRequest();
            }

            var existing = (await store.GetAllAsync()).FirstOrDefault(o => o.Id == id);
            if (existing is null)
            {
                return Results.NotFound();
            }

            var previousName = existing.Name;
            var updated = await store.UpdateAsync(id, request.Name.Trim(), request.RicsCode.Trim(), request.SystemUrl.Trim());
            if (updated is null)
            {
                return Results.NotFound();
            }

            // Renaming an active operator changes its queue name (see
            // TsiMessageEndpoints.PartitionKey) — migrate live consumption to the new queue
            // instead of leaving the old one running.
            if (!string.Equals(previousName, updated.Name, StringComparison.Ordinal))
            {
                await consumerCoordinator.StopAsync(previousName);
                await consumerCoordinator.StartAsync(updated);
            }

            return Results.Ok(updated);
        });

        group.MapPatch("/{id:guid}/status", async (
            Guid id,
            SetInfrastructureOperatorActiveRequest request,
            InfrastructureOperatorStore store,
            InfrastructureOperatorConsumerCoordinator consumerCoordinator) =>
        {
            var existing = (await store.GetAllAsync()).FirstOrDefault(o => o.Id == id);
            if (existing is null || !await store.SetActiveAsync(id, request.IsActive))
            {
                return Results.NotFound();
            }

            if (request.IsActive)
            {
                existing.IsActive = true;
                await consumerCoordinator.StartAsync(existing);
            }
            else
            {
                await consumerCoordinator.StopAsync(existing.Name);
            }

            return Results.Ok();
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            InfrastructureOperatorStore store,
            RailwayUndertakingStore railwayUndertakingStore,
            InfrastructureOperatorConsumerCoordinator consumerCoordinator) =>
        {
            var existing = (await store.GetAllAsync()).FirstOrDefault(o => o.Id == id);
            if (existing is null || !await store.DeleteAsync(id))
            {
                return Results.NotFound();
            }

            await railwayUndertakingStore.RemoveInfrastructureOperatorAssignmentsAsync(id);
            await consumerCoordinator.StopAsync(existing.Name);
            return Results.Ok();
        });
    }
}
