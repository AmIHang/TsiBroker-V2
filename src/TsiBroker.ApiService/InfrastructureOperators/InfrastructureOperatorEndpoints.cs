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

        group.MapPost("/", async (CreateInfrastructureOperatorRequest request, InfrastructureOperatorStore store) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name)
                || string.IsNullOrWhiteSpace(request.RicsCode)
                || string.IsNullOrWhiteSpace(request.SystemUrl))
            {
                return Results.BadRequest();
            }

            var created = await store.AddAsync(request.Name.Trim(), request.RicsCode.Trim(), request.SystemUrl.Trim());
            return Results.Created($"/api/infrastructure-operators/{created.Id}", created);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateInfrastructureOperatorRequest request, InfrastructureOperatorStore store) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name)
                || string.IsNullOrWhiteSpace(request.RicsCode)
                || string.IsNullOrWhiteSpace(request.SystemUrl))
            {
                return Results.BadRequest();
            }

            var updated = await store.UpdateAsync(id, request.Name.Trim(), request.RicsCode.Trim(), request.SystemUrl.Trim());
            return updated is not null ? Results.Ok(updated) : Results.NotFound();
        });

        group.MapPatch("/{id:guid}/status", async (Guid id, SetInfrastructureOperatorActiveRequest request, InfrastructureOperatorStore store) =>
            await store.SetActiveAsync(id, request.IsActive) ? Results.Ok() : Results.NotFound());

        group.MapDelete("/{id:guid}", async (Guid id, InfrastructureOperatorStore store, RailwayUndertakingStore railwayUndertakingStore) =>
        {
            if (!await store.DeleteAsync(id))
            {
                return Results.NotFound();
            }

            await railwayUndertakingStore.RemoveInfrastructureOperatorAssignmentsAsync(id);
            return Results.Ok();
        });
    }
}
