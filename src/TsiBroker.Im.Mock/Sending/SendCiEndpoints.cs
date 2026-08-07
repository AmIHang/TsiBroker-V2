using Microsoft.Extensions.Options;
using TsiBroker.Im.Mock.Storage;

namespace TsiBroker.Im.Mock.Sending;

public static class SendCiEndpoints
{
    public static void MapSendCiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/send").RequireAuthorization();

        group.MapGet("/defaults", (IOptions<CiClientOptions> options) =>
            Results.Ok(new SendDefaults(options.Value.TargetUrl, options.Value.DefaultSenderRics)));

        group.MapPost("", async (
            SendRequest request,
            CiClient client,
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
                messageIdentifier: request.MessageIdentifier,
                messageLiHost: request.MessageLiHost,
                senderAlias: request.SenderAlias,
                cancellationToken: cancellationToken);

            await store.SaveSentAsync(
                request.Payload,
                request.MessageIdentifier,
                result.Status,
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
        string? MessageLiHost = null,
        string? SenderAlias = null);

    public record SendDefaults(string TargetUrl, string SenderRics);
}
