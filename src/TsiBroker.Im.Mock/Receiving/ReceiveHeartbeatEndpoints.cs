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
        app.MapPost("/heartbeat", () => Results.Text(
            """<UICHBMessageResponse xmlns="http://uic.cc.org/UICMessage"><return>HEART_BEAT_WS_RECEIVED</return></UICHBMessageResponse>""",
            "application/xml"));
    }
}
