using System.Xml.Linq;
using TsiBroker.Ru.Mock.Storage;

namespace TsiBroker.Ru.Mock.Receiving;

/// <summary>
/// Plays the EVU side of the RU REST contract once TsiBroker's outbound relay exists: the
/// broker will POST here (mirroring the real TsiBroker.Ru.Api /message contract, see
/// External-API-Guide) instead of the mock POSTing to the broker. Deliberately takes the raw
/// message XML without requiring the X-Api-Key the real endpoint checks - the outbound relay
/// contract doesn't exist yet, so there is no real wire format to be faithful to. See
/// infrastructure/evu-endpoints.openapi.yaml for the documented contract - keep both in sync
/// whenever either changes.
/// </summary>
public static class ReceiveMessageEndpoints
{
    public static void MapReceiveMessageEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/message", async (
            HttpRequest request,
            MockMessageStore store,
            ResponseConfigStore configStore,
            CancellationToken cancellationToken) =>
        {
            using var reader = new StreamReader(request.Body);
            var rawXml = await reader.ReadToEndAsync(cancellationToken);
            var messageIdentifier = MessageIdentifierParser.TryExtract(rawXml);

            var config = configStore.Current;
            if (config.DelayMs > 0)
            {
                await Task.Delay(config.DelayMs, cancellationToken);
            }

            if (config.Mode == MockResponseMode.HttpError)
            {
                await store.SaveReceivedAsync(rawXml, messageIdentifier, "HttpError", cancellationToken);
                return XmlProblem(
                    title: "Simulated EVU failure",
                    detail: "The mock is currently configured to fail every incoming message.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            if (config.Mode == MockResponseMode.Unauthorized)
            {
                await store.SaveReceivedAsync(rawXml, messageIdentifier, "Unauthorized", cancellationToken);
                return XmlProblem(
                    title: "Simulated invalid API key",
                    detail: "The mock is currently configured to reject every incoming message as if the "
                        + "broker presented an invalid or missing API key.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            if (config.Mode == MockResponseMode.Forbidden)
            {
                await store.SaveReceivedAsync(rawXml, messageIdentifier, "Forbidden", cancellationToken);
                return XmlProblem(
                    title: "Simulated missing permissions",
                    detail: "The mock is currently configured to reject every incoming message as not "
                        + "authorized (message type not allowed for this EVU/ISB assignment).",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            var status = config.Mode == MockResponseMode.Nack ? "NACK" : "ACK";
            await store.SaveReceivedAsync(rawXml, messageIdentifier, status, cancellationToken);

            return XmlAccepted(status, messageIdentifier);
        });
    }

    // Request is XML, so the response is XML too - mirrors TsiBroker.Ru.Api's own /message (see
    // the comment there, including why the root element is MessageResponse not AckResponse) and
    // infrastructure/evu-endpoints.openapi.yaml.
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

    private static IResult XmlProblem(string title, string detail, int statusCode)
    {
        var xml = new XElement(
            "ProblemDetails",
            new XElement("Status", statusCode),
            new XElement("Title", title),
            new XElement("Detail", detail));

        return Results.Text(xml.ToString(SaveOptions.DisableFormatting), "application/xml", statusCode: statusCode);
    }
}
