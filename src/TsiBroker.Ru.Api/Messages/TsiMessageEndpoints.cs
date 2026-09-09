using System.Xml.Linq;
using TsiBroker.Core.Messaging;
using TsiBroker.Core.RailwayUndertakings;

namespace TsiBroker.Ru.Api.Messages;

// The RU-facing contract this and WhoAmIEndpoints expose is documented in
// infrastructure/broker-ru-endpoints.openapi.yaml and wiki/External-API-Guide.md - keep all three
// in sync when a route/status/response shape changes here.
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
                return XmlProblem(
                    title: "Missing API key",
                    detail: $"The {ApiKeyHeader.Name} header is required.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var apiKey = apiKeyValues.ToString();

            using var reader = new StreamReader(request.Body);
            var rawXml = await reader.ReadToEndAsync();

            if (!IncomingTsiMessageParser.TryParse(rawXml, out var message, out var parseError))
            {
                return XmlProblem(
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

            return XmlAccepted(status: "ACK", messageIdentifier: message.MessageIdentifier);
        });
    }

    private static IResult ToProblemResult(TsiMessageAuthorizationFailureReason reason) => reason switch
    {
        TsiMessageAuthorizationFailureReason.InvalidApiKey => XmlProblem(
            title: "Invalid API key",
            detail: "No active EVU was found for the supplied API key.",
            statusCode: StatusCodes.Status401Unauthorized),
        TsiMessageAuthorizationFailureReason.SenderMismatch => XmlProblem(
            title: "Sender mismatch",
            detail: "The message Sender does not match a RICS code of the authenticated EVU.",
            statusCode: StatusCodes.Status403Forbidden),
        TsiMessageAuthorizationFailureReason.UnknownRecipient => XmlProblem(
            title: "Unknown recipient",
            detail: "The message Recipient does not match a known, active infrastructure operator.",
            statusCode: StatusCodes.Status400BadRequest),
        TsiMessageAuthorizationFailureReason.NoIsbAssignment => XmlProblem(
            title: "EVU not authorized for this ISB",
            detail: "The authenticated EVU has no active assignment for the message recipient.",
            statusCode: StatusCodes.Status403Forbidden),
        TsiMessageAuthorizationFailureReason.MessageTypeNotAllowed => XmlProblem(
            title: "Message type not authorized",
            detail: "The authenticated EVU is not authorized to send this message type to this recipient.",
            statusCode: StatusCodes.Status403Forbidden),
        _ => XmlProblem(title: null, detail: null, statusCode: StatusCodes.Status403Forbidden),
    };

    // Request is XML (the raw TAF/TAP-TSI payload), so the response is XML too, rather than
    // switching formats mid-contract - see wiki/External-API-Guide.md#post-message and
    // infrastructure/evu-endpoints.openapi.yaml, which this mirrors. Root element name is
    // MessageResponse, not the more descriptive AckResponse, because real EVU clients (e.g.
    // CommunityOperations.Interfaces' TsiBrokerClientService.SendMessage) deserialize with a bare
    // XmlSerializer against a MessageResponse-named class with no [XmlRoot] override, so
    // XmlSerializer defaults to matching on the class/root name.
    private static IResult XmlAccepted(string status, string? messageIdentifier)
    {
        var xml = new XElement(
            "MessageResponse",
            new XElement("Status", status),
            messageIdentifier is null ? null : new XElement("MessageIdentifier", messageIdentifier));

        return Results.Text(
            xml.ToString(SaveOptions.DisableFormatting),
            "application/xml",
            statusCode: StatusCodes.Status202Accepted);
    }

    private static IResult XmlProblem(string? title, string? detail, int statusCode)
    {
        var xml = new XElement(
            "ProblemDetails",
            new XElement("Status", statusCode),
            title is null ? null : new XElement("Title", title),
            detail is null ? null : new XElement("Detail", detail));

        return Results.Text(xml.ToString(SaveOptions.DisableFormatting), "application/xml", statusCode: statusCode);
    }
}
