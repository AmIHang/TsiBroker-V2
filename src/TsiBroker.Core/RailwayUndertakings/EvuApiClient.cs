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
        SendAsync(HttpMethod.Get, railwayUndertaking, "config/update", cancellationToken);

    private Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        RailwayUndertaking railwayUndertaking,
        string relativePath,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, BuildUri(railwayUndertaking.SystemUrl, relativePath));
        request.Headers.Add(ApiKeyHeaderName, railwayUndertaking.ApiKeyBrokerToEvu);
        return httpClient.SendAsync(request, cancellationToken);
    }

    // Uri combining treats a base without a trailing slash as a "file", dropping its last
    // path segment (e.g. "https://evu/api" + "config/update" => "https://evu/config/update").
    private static Uri BuildUri(string systemUrl, string relativePath)
    {
        var baseUrl = systemUrl.EndsWith('/') ? systemUrl : systemUrl + "/";
        return new Uri(new Uri(baseUrl), relativePath);
    }
}
