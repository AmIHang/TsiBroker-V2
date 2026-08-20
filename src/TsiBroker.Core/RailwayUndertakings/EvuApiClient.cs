using System.Text;
using TsiBroker.Core.Messaging;

namespace TsiBroker.Core.RailwayUndertakings;

// Outbound REST calls from the broker to an EVU's own system (RailwayUndertaking.SystemUrl),
// authenticated with the key the broker was issued for that EVU (ApiKeyBrokerToEvu) - the
// mirror image of the inbound EVU->broker auth used by TsiBroker.Ru.Api.
public class EvuApiClient(HttpClient httpClient)
{
    private const string ApiKeyHeaderName = "X-Api-Key";

    public Task<HttpResponseMessage> TriggerConfigUpdateAsync(
        RailwayUndertaking railwayUndertaking,
        CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, railwayUndertaking, "config/update", cancellationToken);

    // POST /message — delivers the raw TSI XML content to the EVU's own system. See
    // infrastructure/evu-endpoints.openapi.yaml for the documented contract.
    public async Task<HttpResponseMessage> DeliverMessageAsync(
        RailwayUndertaking railwayUndertaking,
        BrokerMessage message,
        CancellationToken cancellationToken = default)
    {
        // Must await inside the `using` scope — returning the un-awaited SendAsync task would
        // dispose `request` (and its StringContent) as soon as this method returns, racing the
        // still-in-flight write of the request body to the network stream.
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(railwayUndertaking.SystemUrl, "message"))
        {
            Content = new StringContent(message.Content, Encoding.UTF8, "application/xml"),
        };
        request.Headers.Add(ApiKeyHeaderName, railwayUndertaking.ApiKeyBrokerToEvu);
        return await httpClient.SendAsync(request, cancellationToken);
    }

    // GET /health — a pure reachability probe, deliberately unauthenticated (see
    // TsiBroker.Ru.Mock/Receiving/HealthEndpoints.cs). Never throws: any failure to connect or
    // a non-2xx response both mean "not reachable".
    public async Task<bool> CheckHealthAsync(
        RailwayUndertaking railwayUndertaking,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(railwayUndertaking.SystemUrl, "health"));
            using var response = await httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        RailwayUndertaking railwayUndertaking,
        string relativePath,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, BuildUri(railwayUndertaking.SystemUrl, relativePath));
        request.Headers.Add(ApiKeyHeaderName, railwayUndertaking.ApiKeyBrokerToEvu);
        return await httpClient.SendAsync(request, cancellationToken);
    }

    // Uri combining treats a base without a trailing slash as a "file", dropping its last
    // path segment (e.g. "https://evu/api" + "config/update" => "https://evu/config/update").
    private static Uri BuildUri(string systemUrl, string relativePath)
    {
        var baseUrl = systemUrl.EndsWith('/') ? systemUrl : systemUrl + "/";
        return new Uri(new Uri(baseUrl), relativePath);
    }
}
