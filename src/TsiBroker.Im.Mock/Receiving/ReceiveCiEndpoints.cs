using TsiBroker.Im.Core.CI;
using TsiBroker.Im.Mock.Storage;

namespace TsiBroker.Im.Mock.Receiving;

/// <summary>
/// Plays the ISB side of the Common Interface contract once TsiBroker's outbound relay exists:
/// the broker will POST here instead of the mock POSTing to the broker. Deliberately takes the
/// raw message XML without a SOAP envelope - the outbound relay contract doesn't exist yet, so
/// there is no real wire format to be faithful to.
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
}
