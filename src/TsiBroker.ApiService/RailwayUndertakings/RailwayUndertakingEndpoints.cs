namespace TsiBroker.ApiService.RailwayUndertakings;

public record CreateRailwayUndertakingRequest(string Name, List<string> RicsCodes, string SystemUrl);

public record UpdateRailwayUndertakingRequest(
    string Name,
    List<string> RicsCodes,
    string SystemUrl,
    string ApiKeyEvuToBroker,
    string ApiKeyBrokerToEvu);

public record SetRailwayUndertakingActiveRequest(bool IsActive);

public static class RailwayUndertakingEndpoints
{
    public static void MapRailwayUndertakingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/railway-undertakings").RequireAuthorization();

        group.MapGet("/", async (RailwayUndertakingStore store) =>
            Results.Ok(await store.GetAllAsync()));

        group.MapPost("/", async (CreateRailwayUndertakingRequest request, RailwayUndertakingStore store) =>
        {
            var ricsCodes = NormalizeRicsCodes(request.RicsCodes);
            if (string.IsNullOrWhiteSpace(request.Name)
                || ricsCodes.Count == 0
                || string.IsNullOrWhiteSpace(request.SystemUrl))
            {
                return Results.BadRequest();
            }

            var created = await store.AddAsync(request.Name.Trim(), ricsCodes, request.SystemUrl.Trim());
            return Results.Created($"/api/railway-undertakings/{created.Id}", created);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateRailwayUndertakingRequest request, RailwayUndertakingStore store) =>
        {
            var ricsCodes = NormalizeRicsCodes(request.RicsCodes);
            if (string.IsNullOrWhiteSpace(request.Name)
                || ricsCodes.Count == 0
                || string.IsNullOrWhiteSpace(request.SystemUrl))
            {
                return Results.BadRequest();
            }

            var updated = await store.UpdateAsync(
                id,
                request.Name.Trim(),
                ricsCodes,
                request.SystemUrl.Trim(),
                request.ApiKeyEvuToBroker.Trim(),
                request.ApiKeyBrokerToEvu.Trim());
            return updated is not null ? Results.Ok(updated) : Results.NotFound();
        });

        group.MapPatch("/{id:guid}/status", async (Guid id, SetRailwayUndertakingActiveRequest request, RailwayUndertakingStore store) =>
            await store.SetActiveAsync(id, request.IsActive) ? Results.Ok() : Results.NotFound());

        group.MapPost("/{id:guid}/api-key-evu-to-broker/regenerate", async (Guid id, RailwayUndertakingStore store) =>
        {
            var updated = await store.RegenerateApiKeyEvuToBrokerAsync(id);
            return updated is not null ? Results.Ok(updated) : Results.NotFound();
        });

        group.MapPost("/{id:guid}/api-key-broker-to-evu/regenerate", async (Guid id, RailwayUndertakingStore store) =>
        {
            var updated = await store.RegenerateApiKeyBrokerToEvuAsync(id);
            return updated is not null ? Results.Ok(updated) : Results.NotFound();
        });

        group.MapDelete("/{id:guid}", async (Guid id, RailwayUndertakingStore store) =>
            await store.DeleteAsync(id) ? Results.Ok() : Results.NotFound());
    }

    private static List<string> NormalizeRicsCodes(List<string>? ricsCodes) =>
        ricsCodes is null
            ? []
            : ricsCodes
                .Select(code => code.Trim())
                .Where(code => code.Length > 0)
                .Distinct()
                .ToList();
}
