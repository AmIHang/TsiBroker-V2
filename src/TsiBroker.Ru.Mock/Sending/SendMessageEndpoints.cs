using Microsoft.Extensions.Options;
using TsiBroker.Ru.Mock.Storage;

namespace TsiBroker.Ru.Mock.Sending;

public static class SendMessageEndpoints
{
    public static void MapSendMessageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/send").RequireAuthorization();

        group.MapGet("/defaults", (IOptions<RuClientOptions> options) =>
            Results.Ok(new SendDefaults(options.Value.TargetUrl, options.Value.DefaultSenderRics, options.Value.ApiKey)));

        group.MapPost("", async (
            SendRequest request,
            RuClient client,
            MockMessageStore store,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Payload))
            {
                return Results.Problem(
                    title: "Missing payload",
                    detail: "Payload must contain the message XML to send.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var result = await client.SendAsync(
                payload: request.Payload,
                targetUrl: request.TargetUrl,
                apiKey: request.ApiKey,
                cancellationToken: cancellationToken);

            await store.SaveSentAsync(
                request.Payload,
                request.MessageIdentifier,
                result.Status,
                result.ResponseBody,
                cancellationToken);

            return Results.Ok(result);
        });

        group.MapGet("/templates", () =>
            Results.Ok(MessageTemplates.ByMessageType.Keys.OrderBy(k => k, StringComparer.Ordinal)));

        group.MapGet("/templates/{messageType}", (string messageType) =>
            MessageTemplates.ByMessageType.TryGetValue(messageType, out var xml)
                ? Results.Text(xml, "application/xml")
                : Results.NotFound());
    }

    public record SendRequest(
        string Payload,
        string? TargetUrl = null,
        string? MessageIdentifier = null,
        string? ApiKey = null);

    public record SendDefaults(string TargetUrl, string SenderRics, string ApiKey);
}
