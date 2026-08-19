using TsiBroker.Core.Messaging;
using TsiBroker.Core.RailwayUndertakings;

namespace TsiBroker.Ru.Api.Messages;

public static class TsiMessageEndpoints
{
    public static void MapTsiMessageEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/message", async (
            HttpRequest request,
            TsiMessageAuthorizationService authorizationService,
            IMessagePublisher publisher,
            RailwayUndertakingStore railwayUndertakingStore) =>
        {
            if (!request.Headers.TryGetValue(ApiKeyHeader.Name, out var apiKeyValues)
                || string.IsNullOrWhiteSpace(apiKeyValues.ToString()))
            {
                return Results.Problem(
                    title: "Missing API key",
                    detail: $"The {ApiKeyHeader.Name} header is required.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var apiKey = apiKeyValues.ToString();

            using var reader = new StreamReader(request.Body);
            var rawXml = await reader.ReadToEndAsync();

            if (!IncomingTsiMessageParser.TryParse(rawXml, out var message, out var parseError))
            {
                return Results.Problem(
                    title: "Invalid message",
                    detail: parseError,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var authorization = await authorizationService.AuthorizeAsync(apiKey, message!);
            if (!authorization.IsSuccess)
            {
                return ToProblemResult(authorization.FailureReason!.Value);
            }

            // An authenticated request from this EVU is itself evidence it's back online — clear
            // a paused outbound queue so TsiBroker.ApiService's EvuReachabilityMonitor (a
            // separate process) picks it up and resumes delivery, without waiting out the rest
            // of the current backoff interval.
            if (authorization.RailwayUndertaking!.IsQueuePaused)
            {
                await railwayUndertakingStore.SetQueuePauseStateAsync(
                    authorization.RailwayUndertaking.Id, isPaused: false, reason: null, backoffStep: 0);
            }

            var brokerMessage = new BrokerMessage(
                Id: message!.MessageIdentifier,
                Sender: message.Sender,
                Receiver: message.Recipient,
                Content: rawXml,
                CreatedAt: DateTimeOffset.UtcNow,
                // Groups all messages addressed to this Infrastrukturbetreiber into one
                // queue, regardless of which RU/RicsCode sent them — see
                // IMessageConsumer.RunPartitionedAsync.
                PartitionKey: authorization.InfrastructureOperator!.Name,
                MessageType: message.MessageType);

            await publisher.PublishAsync(brokerMessage);

            return Results.Accepted(value: new { status = "ACK", messageIdentifier = message.MessageIdentifier });
        });
    }

    private static IResult ToProblemResult(TsiMessageAuthorizationFailureReason reason) => reason switch
    {
        TsiMessageAuthorizationFailureReason.InvalidApiKey => Results.Problem(
            title: "Invalid API key",
            detail: "No active EVU was found for the supplied API key.",
            statusCode: StatusCodes.Status401Unauthorized),
        TsiMessageAuthorizationFailureReason.SenderMismatch => Results.Problem(
            title: "Sender mismatch",
            detail: "The message Sender does not match a RICS code of the authenticated EVU.",
            statusCode: StatusCodes.Status403Forbidden),
        TsiMessageAuthorizationFailureReason.UnknownRecipient => Results.Problem(
            title: "Unknown recipient",
            detail: "The message Recipient does not match a known, active infrastructure operator.",
            statusCode: StatusCodes.Status400BadRequest),
        TsiMessageAuthorizationFailureReason.NoIsbAssignment => Results.Problem(
            title: "EVU not authorized for this ISB",
            detail: "The authenticated EVU has no active assignment for the message recipient.",
            statusCode: StatusCodes.Status403Forbidden),
        TsiMessageAuthorizationFailureReason.MessageTypeNotAllowed => Results.Problem(
            title: "Message type not authorized",
            detail: "The authenticated EVU is not authorized to send this message type to this recipient.",
            statusCode: StatusCodes.Status403Forbidden),
        _ => Results.Problem(statusCode: StatusCodes.Status403Forbidden),
    };
}
