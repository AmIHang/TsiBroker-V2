namespace TsiBroker.Im.Mock.Receiving;

public static class ResponseConfigEndpoints
{
    public static void MapResponseConfigEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/response-config").RequireAuthorization();

        group.MapGet("", (ResponseConfigStore store) =>
            Results.Ok(ToDto(store.Current)));

        group.MapPut("", (ResponseConfigStore store, ResponseConfigDto dto) =>
        {
            if (!Enum.TryParse<MockResponseMode>(dto.Mode, ignoreCase: true, out var mode))
            {
                return Results.Problem(
                    title: "Invalid mode",
                    detail: $"Mode must be one of: {string.Join(", ", Enum.GetNames<MockResponseMode>())}.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            store.Current = new ResponseConfig(mode, Math.Max(0, dto.DelayMs));
            return Results.Ok(ToDto(store.Current));
        });
    }

    private static ResponseConfigDto ToDto(ResponseConfig config) => new(config.Mode.ToString(), config.DelayMs);

    public record ResponseConfigDto(string Mode, int DelayMs = 0);
}
