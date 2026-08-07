using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace TsiBroker.Ru.Mock.Sending;

/// <summary>
/// Client side of the RU REST contract: posts the raw TAF/TAP message XML straight to
/// TsiBroker.Ru.Api's /message endpoint with the X-Api-Key header it requires, playing the EVU
/// sending a message to the broker. Unlike TsiBroker.Im.Mock's CiClient there is no envelope to
/// build - the real endpoint takes the message XML as-is (see TsiMessageEndpoints). Also covers
/// GET /whoami, a second, unrelated route (no payload, XML response) used to sanity-check this
/// EVU's admin-configured assignments rather than to exchange a message.
/// </summary>
public class RuClient(HttpClient httpClient, IOptions<RuClientOptions> options)
{
    private const string ApiKeyHeaderName = "X-Api-Key";

    public async Task<RuSendResult> SendAsync(
        string payload,
        string? targetUrl = null,
        string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        using var content = new StringContent(payload, Encoding.UTF8, "application/xml");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, targetUrl ?? options.Value.TargetUrl)
            {
                Content = content,
            };
            request.Headers.TryAddWithoutValidation(
                ApiKeyHeaderName,
                string.IsNullOrWhiteSpace(apiKey) ? options.Value.ApiKey : apiKey);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var title = ExtractJsonProperty(responseBody, "title") ?? $"HTTP {(int)response.StatusCode}";
                return new RuSendResult(false, "HttpError", responseBody, title);
            }

            var status = ExtractJsonProperty(responseBody, "status") ?? "Unknown";
            return new RuSendResult(true, status, responseBody, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new RuSendResult(false, "Unreachable", null, ex.Message);
        }
    }

    public async Task<WhoAmIResult> WhoAmIAsync(
        string? url = null,
        string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url ?? options.Value.WhoAmIUrl);
            request.Headers.TryAddWithoutValidation(
                ApiKeyHeaderName,
                string.IsNullOrWhiteSpace(apiKey) ? options.Value.ApiKey : apiKey);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new WhoAmIResult(false, responseBody, $"HTTP {(int)response.StatusCode}");
            }

            return new WhoAmIResult(true, responseBody, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new WhoAmIResult(false, null, ex.Message);
        }
    }

    private static string? ExtractJsonProperty(string json, string propertyName)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty(propertyName, out var value)
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
