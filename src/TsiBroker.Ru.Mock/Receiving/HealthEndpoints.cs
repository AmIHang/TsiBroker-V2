namespace TsiBroker.Ru.Mock.Receiving;

/// <summary>
/// Lets the broker check whether this EVU is reachable at all - unlike /message and
/// /config/update, this never applies the simulated response mode (delay/error/nack) and does
/// not require the X-Api-Key those endpoints expect, so it stays a pure reachability probe. See
/// infrastructure/evu-endpoints.openapi.yaml for the documented contract - keep both in sync
/// whenever either changes.
/// </summary>
public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok());
    }
}
