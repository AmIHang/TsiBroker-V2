using Microsoft.Extensions.Options;
using TsiBroker.Ru.Mock.Sending;

namespace TsiBroker.Ru.Mock.Diagnostics;

/// <summary>
/// Proxies GET /whoami on the configured broker, so the mock's own admin cookie session can be
/// used to check this EVU's admin-configured assignments without exposing the broker's CORS to
/// the browser directly (TsiBroker.Ru.Api has none). A read-only config check, not a message
/// exchange - kept separate from Sending/Receiving for that reason, even though it shares
/// RuClient's HttpClient/options.
/// </summary>
public static class WhoAmIEndpoints
{
    public static void MapWhoAmIEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/whoami").RequireAuthorization();

        group.MapGet("/defaults", (IOptions<RuClientOptions> options) =>
            Results.Ok(new WhoAmIDefaults(options.Value.WhoAmIUrl, options.Value.ApiKey)));

        group.MapPost("", async (WhoAmIRequest request, RuClient client, CancellationToken cancellationToken) =>
        {
            var result = await client.WhoAmIAsync(request.Url, request.ApiKey, cancellationToken);
            return Results.Ok(result);
        });
    }

    public record WhoAmIRequest(string? Url = null, string? ApiKey = null);

    public record WhoAmIDefaults(string Url, string ApiKey);
}
