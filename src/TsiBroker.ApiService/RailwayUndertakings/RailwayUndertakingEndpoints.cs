using TsiBroker.ApiService.InfrastructureOperators;
using TsiBroker.Core.RailwayUndertakings;

namespace TsiBroker.ApiService.RailwayUndertakings;

public record InfrastructureOperatorAssignmentRequest(
    Guid InfrastructureOperatorId,
    List<string> AllowedMessageTypesEvuToBroker,
    List<string> AllowedMessageTypesBrokerToEvu,
    bool IsActive);

public record CreateRailwayUndertakingRequest(
    string Name,
    List<string> RicsCodes,
    string SystemUrl,
    List<InfrastructureOperatorAssignmentRequest> InfrastructureOperatorAssignments);

public record UpdateRailwayUndertakingRequest(
    string Name,
    List<string> RicsCodes,
    string SystemUrl,
    string ApiKeyEvuToBroker,
    string ApiKeyBrokerToEvu,
    List<InfrastructureOperatorAssignmentRequest> InfrastructureOperatorAssignments);

public record SetRailwayUndertakingActiveRequest(bool IsActive);

public static class RailwayUndertakingEndpoints
{
    public static void MapRailwayUndertakingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/railway-undertakings").RequireAuthorization();

        group.MapGet("/", async (RailwayUndertakingStore store) =>
            Results.Ok(await store.GetAllAsync()));

        group.MapPost("/", async (CreateRailwayUndertakingRequest request, RailwayUndertakingStore store, InfrastructureOperatorStore isbStore) =>
        {
            var ricsCodes = NormalizeRicsCodes(request.RicsCodes);
            if (string.IsNullOrWhiteSpace(request.Name)
                || ricsCodes.Count == 0
                || string.IsNullOrWhiteSpace(request.SystemUrl))
            {
                return Results.BadRequest();
            }

            var assignments = await NormalizeAssignmentsAsync(request.InfrastructureOperatorAssignments, isbStore);
            if (assignments is null)
            {
                return Results.BadRequest();
            }

            var created = await store.AddAsync(request.Name.Trim(), ricsCodes, request.SystemUrl.Trim(), assignments);
            return Results.Created($"/api/railway-undertakings/{created.Id}", created);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateRailwayUndertakingRequest request, RailwayUndertakingStore store, InfrastructureOperatorStore isbStore) =>
        {
            var ricsCodes = NormalizeRicsCodes(request.RicsCodes);
            if (string.IsNullOrWhiteSpace(request.Name)
                || ricsCodes.Count == 0
                || string.IsNullOrWhiteSpace(request.SystemUrl))
            {
                return Results.BadRequest();
            }

            var assignments = await NormalizeAssignmentsAsync(request.InfrastructureOperatorAssignments, isbStore);
            if (assignments is null)
            {
                return Results.BadRequest();
            }

            var updated = await store.UpdateAsync(
                id,
                request.Name.Trim(),
                ricsCodes,
                request.SystemUrl.Trim(),
                request.ApiKeyEvuToBroker.Trim(),
                request.ApiKeyBrokerToEvu.Trim(),
                assignments);
            return updated is not null ? Results.Ok(updated) : Results.NotFound();
        });

        group.MapPatch("/{id:guid}/status", async (Guid id, SetRailwayUndertakingActiveRequest request, RailwayUndertakingStore store) =>
            await store.SetActiveAsync(id, request.IsActive) ? Results.Ok() : Results.NotFound());

        group.MapGet("/generate-api-key", () => Results.Ok(new { apiKey = RailwayUndertakingStore.GenerateApiKey() }));

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

    private static List<string> NormalizeMessageTypes(List<string>? messageTypes) =>
        messageTypes is null
            ? []
            : messageTypes
                .Select(type => type.Trim())
                .Where(type => type.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    private static async Task<List<IsbAssignment>?> NormalizeAssignmentsAsync(
        List<InfrastructureOperatorAssignmentRequest>? assignments,
        InfrastructureOperatorStore isbStore)
    {
        if (assignments is null || assignments.Count == 0)
        {
            return [];
        }

        var existingIds = (await isbStore.GetAllAsync()).Select(o => o.Id).ToHashSet();
        var seenIds = new HashSet<Guid>();
        var normalized = new List<IsbAssignment>();
        foreach (var assignment in assignments)
        {
            if (!existingIds.Contains(assignment.InfrastructureOperatorId) || !seenIds.Add(assignment.InfrastructureOperatorId))
            {
                return null;
            }

            normalized.Add(new IsbAssignment
            {
                InfrastructureOperatorId = assignment.InfrastructureOperatorId,
                AllowedMessageTypesEvuToBroker = NormalizeMessageTypes(assignment.AllowedMessageTypesEvuToBroker),
                AllowedMessageTypesBrokerToEvu = NormalizeMessageTypes(assignment.AllowedMessageTypesBrokerToEvu),
                IsActive = assignment.IsActive,
            });
        }

        return normalized;
    }
}
