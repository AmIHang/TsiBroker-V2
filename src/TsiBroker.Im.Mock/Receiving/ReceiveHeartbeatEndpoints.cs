namespace TsiBroker.Im.Mock.Receiving;

/// <summary>
/// Plays the ISB side of the Heartbeat contract for reachability probes from the broker's
/// InfrastructureOperatorReachabilityMonitor / IsbApiClient.CheckHeartbeatAsync - a pure liveness
/// echo with nothing to store or configure, mirroring
/// TsiBroker.Im.Api/Heartbeat/HeartbeatMessageService.cs for the opposite direction.
/// </summary>
public static class ReceiveHeartbeatEndpoints
{
    public static void MapReceiveHeartbeatEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/heartbeat", (HttpRequest request) =>
        {
            // Same Content-Type enforcement as ReceiveCiEndpoints - see the comment there.
            if (request.ContentType is not { } requestContentType
                || !requestContentType.StartsWith("application/xml", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Problem(
                    title: "Unsupported content type",
                    detail: $"Expected 'application/xml; charset=UTF-8', got '{request.ContentType}'.",
                    statusCode: StatusCodes.Status415UnsupportedMediaType);
            }

            return Results.Text(
                """<UICHBMessageResponse xmlns="http://uic.cc.org/UICMessage"><return>HEART_BEAT_WS_RECEIVED</return></UICHBMessageResponse>""",
                "application/xml");
        });
    }
}
