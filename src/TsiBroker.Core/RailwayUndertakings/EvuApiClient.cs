using System.Text;
using TsiBroker.Core.Messaging;

namespace TsiBroker.Core.RailwayUndertakings;

// A /message delivery attempt (see EvuApiClient.DeliverMessageAsync) can fail for two very
// different reasons — EvuDeliveryCoordinator.HandleMessageAsync reacts to each differently rather
// than collapsing both into a plain success/failure bool:
public enum EvuDeliveryOutcome
{
    // 2xx — the EVU accepted the message.
    Delivered,
    // A response was received but it wasn't 2xx (e.g. 400 malformed XML, 401/403 auth, or a
    // simulated 5xx — see infrastructure/evu-endpoints.openapi.yaml) — the EVU actively processed
    // and rejected the message. This is an application-level, per-message failure, not a sign the
    // EVU is unreachable, so it's dead-lettered immediately: no retry, no reachability check, no
    // queue pause.
    Rejected,
    // No response was received at all (connection failure or timeout) — the only case that
    // warrants checking reachability and possibly pausing the queue, since it's the only one that
    // actually indicates the EVU might be down.
    TransportFailure,
}

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

    // POST /message — delivers the raw TSI XML content to the EVU's own system, classifying the
    // outcome per EvuDeliveryOutcome rather than just returning the raw response. See
    // infrastructure/evu-endpoints.openapi.yaml for the documented contract.
    public async Task<EvuDeliveryOutcome> DeliverMessageAsync(
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

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return EvuDeliveryOutcome.TransportFailure;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return EvuDeliveryOutcome.TransportFailure;
        }

        using (response)
        {
            return response.IsSuccessStatusCode ? EvuDeliveryOutcome.Delivered : EvuDeliveryOutcome.Rejected;
        }
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
