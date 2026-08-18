using System.Xml;
using System.Xml.Linq;
using TsiBroker.Im.Core.CI;
using TsiBroker.Im.Mock.Storage;

namespace TsiBroker.Im.Mock.Receiving;

/// <summary>
/// Plays the ISB side of the Common Interface contract: the broker's outbound relay
/// (TsiBroker.ApiService's InfrastructureOperatorConsumerCoordinator, via
/// TsiBroker.Core/InfrastructureOperators/IsbApiClient.cs) posts the same UICMessage SOAP
/// envelope here that TsiBroker.Im.Api's own /ci endpoint expects from a real IM.
/// </summary>
public static class ReceiveCiEndpoints
{
    public static void MapReceiveCiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/ci", async (
            HttpRequest request,
            MockMessageStore store,
            ResponseConfigStore configStore,
            CancellationToken cancellationToken) =>
        {
            using var reader = new StreamReader(request.Body);
            var envelopeXml = await reader.ReadToEndAsync(cancellationToken);
            var rawXml = ExtractMessage(envelopeXml) ?? envelopeXml;
            var messageIdentifier = MessageIdentifierParser.TryExtract(rawXml);

            var config = configStore.Current;
            if (config.DelayMs > 0)
            {
                await Task.Delay(config.DelayMs, cancellationToken);
            }

            if (config.Mode == MockResponseMode.HttpError)
            {
                await store.SaveReceivedAsync(rawXml, messageIdentifier, "HttpError", cancellationToken);
                return Results.Problem(
                    title: "Simulated ISB failure",
                    detail: "The mock is currently configured to fail every incoming message.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            var status = config.Mode == MockResponseMode.Nack ? "NACK" : "ACK";
            await store.SaveReceivedAsync(rawXml, messageIdentifier, status, cancellationToken);

            var ack = TechnicalAckFactory.Create(
                responseStatus: status,
                ackIdentifier: Guid.NewGuid().ToString(),
                messageReference: messageIdentifier ?? string.Empty,
                sender: "isb-mock",
                recipient: "broker",
                remoteLiName: "isb-mock",
                remoteLiInstanceNumber: string.Empty,
                messageTransportMechanism: "HTTP");

            return Results.Text(ack.OuterXml, "application/xml");
        });
    }

    // Unwraps the raw TSI message text from the UICMessage SOAP envelope's <message> element
    // (see IsbApiClient.BuildMessageEnvelope) - namespace-agnostic, mirroring
    // CommonInterfaceMessageService.ReceiveAsync's own unwrapping on the opposite direction.
    // Returns null (falling back to the raw body) if the envelope isn't well-formed XML.
    private static string? ExtractMessage(string envelopeXml)
    {
        try
        {
            var document = XDocument.Parse(envelopeXml);
            return document.Descendants().FirstOrDefault(e => e.Name.LocalName == "message")?.Value;
        }
        catch (XmlException)
        {
            return null;
        }
    }
}
