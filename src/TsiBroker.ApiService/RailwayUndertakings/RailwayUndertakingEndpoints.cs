using TsiBroker.Core.InfrastructureOperators;
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

        group.MapPost("/", async (
            CreateRailwayUndertakingRequest request,
            RailwayUndertakingStore store,
            InfrastructureOperatorStore isbStore) =>
        {
            var ricsCodes = NormalizeRicsCodes(request.RicsCodes);
            if (string.IsNullOrWhiteSpace(request.Name)
                || ricsCodes.Count == 0
                || string.IsNullOrWhiteSpace(request.SystemUrl))
            {
                return Results.BadRequest();
            }

            var duplicateRicsCode = FindDuplicateRicsCode(ricsCodes, await store.GetAllAsync(), excludeId: null);
            if (duplicateRicsCode is not null)
            {
                return Results.Conflict(new { error = "duplicate_rics_code", ricsCode = duplicateRicsCode });
            }

            var assignments = await NormalizeAssignmentsAsync(request.InfrastructureOperatorAssignments, isbStore);
            if (assignments is null)
            {
                return Results.BadRequest();
            }

            var created = await store.AddAsync(request.Name.Trim(), ricsCodes, request.SystemUrl.Trim(), assignments);
            return Results.Created($"/api/railway-undertakings/{created.Id}", created);
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateRailwayUndertakingRequest request,
            RailwayUndertakingStore store,
            InfrastructureOperatorStore isbStore) =>
        {
            var ricsCodes = NormalizeRicsCodes(request.RicsCodes);
            if (string.IsNullOrWhiteSpace(request.Name)
                || ricsCodes.Count == 0
                || string.IsNullOrWhiteSpace(request.SystemUrl))
            {
                return Results.BadRequest();
            }

            var allUndertakings = await store.GetAllAsync();
            var existing = allUndertakings.FirstOrDefault(u => u.Id == id);
            if (existing is null)
            {
                return Results.NotFound();
            }

            var duplicateRicsCode = FindDuplicateRicsCode(ricsCodes, allUndertakings, excludeId: id);
            if (duplicateRicsCode is not null)
            {
                return Results.Conflict(new { error = "duplicate_rics_code", ricsCode = duplicateRicsCode });
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
            if (updated is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(updated);
        });

        group.MapPatch("/{id:guid}/status", async (
            Guid id,
            SetRailwayUndertakingActiveRequest request,
            RailwayUndertakingStore store) =>
        {
            var existing = (await store.GetAllAsync()).FirstOrDefault(u => u.Id == id);
            if (existing is null || !await store.SetActiveAsync(id, request.IsActive))
            {
                return Results.NotFound();
            }

            return Results.Ok();
        });

        group.MapPost("/{id:guid}/trigger-config-update", async (Guid id, RailwayUndertakingStore store, EvuApiClient evuApiClient) =>
        {
            var undertakings = await store.GetAllAsync();
            var undertaking = undertakings.FirstOrDefault(u => u.Id == id);
            if (undertaking is null)
            {
                return Results.NotFound();
            }

            try
            {
                using var response = await evuApiClient.TriggerConfigUpdateAsync(undertaking);
                return response.IsSuccessStatusCode
                    ? Results.Ok()
                    : Results.Problem(
                        title: "EVU request failed",
                        detail: $"The EVU responded with status {(int)response.StatusCode}.",
                        statusCode: StatusCodes.Status502BadGateway);
            }
            catch (HttpRequestException ex)
            {
                return Results.Problem(
                    title: "EVU unreachable",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status502BadGateway);
            }
        });

        group.MapGet("/generate-api-key", () => Results.Ok(new { apiKey = RailwayUndertakingStore.GenerateApiKey() }));

        group.MapDelete("/{id:guid}", async (
            Guid id,
            RailwayUndertakingStore store) =>
        {
            var existing = (await store.GetAllAsync()).FirstOrDefault(u => u.Id == id);
            if (existing is null || !await store.DeleteAsync(id))
            {
                return Results.NotFound();
            }

            return Results.Ok();
        });
    }

    private static List<string> NormalizeRicsCodes(List<string>? ricsCodes) =>
        ricsCodes is null
            ? []
            : ricsCodes
                .Select(code => code.Trim())
                .Where(code => code.Length > 0)
                .Distinct()
                .ToList();

    // RicsCodes must be unique across EVUs: the same code cannot be assigned to two different
    // railway undertakings. ISBs use a separate namespace and are intentionally not checked here.
    private static string? FindDuplicateRicsCode(List<string> ricsCodes, List<RailwayUndertaking> undertakings, Guid? excludeId) =>
        ricsCodes.FirstOrDefault(code =>
            undertakings.Any(u =>
                u.Id != excludeId
                && u.RicsCodes.Any(existingCode => string.Equals(existingCode, code, StringComparison.OrdinalIgnoreCase))));

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
