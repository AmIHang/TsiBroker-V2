using System.Text;
using System.Xml.Linq;
using TsiBroker.Core.Messaging;

namespace TsiBroker.Core.InfrastructureOperators;

// Outbound SOAP calls from the broker to an Infrastrukturbetreiber's own system
// (InfrastructureOperator.SystemUrl) — the mirror image of the inbound IM -> broker SOAP contract
// TsiBroker.Im.Api itself hosts (ICommonInterfaceMessageService / IHeartbeatMessageService).
// Hand-built XML rather than a generated WCF client, the same way TsiBroker.Im.Mock's CiClient
// builds its own UICMessage envelope — there's no partner-supplied WSDL for a generic "any IM" to
// generate a client from.
//
// TODO: real IMs authenticate the broker via a client certificate (mTLS) — not decided/configured
// yet, so this client currently sends plain HTTPS with no client identification.
public class IsbApiClient(HttpClient httpClient)
{
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace HeaderNs = "http://uic.cc.org/UICMessage/Header";
    private static readonly XNamespace MessageNs = "http://uic.cc.org/UICMessage";
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";
    private static readonly XNamespace Xsd = "http://www.w3.org/2001/XMLSchema";

    private const string BrokerAlias = "broker";

    // POST /ci — delivers a TSI message to the IM's Common Interface endpoint, wrapped in the
    // same UICMessage SOAP envelope TsiBroker.Im.Api itself expects (see
    // CommonInterfaceMessageService.ReceiveAsync). Returns whether the IM acknowledged the
    // message (HTTP success and an LI_TechnicalAck ResponseStatus of "ACK") — a NACK is treated
    // by the caller the same as a transport failure (a processing error on the customer's side).
    public async Task<bool> DeliverMessageAsync(
        InfrastructureOperator infrastructureOperator,
        BrokerMessage message,
        CancellationToken cancellationToken = default)
    {
        using var content = new StringContent(BuildMessageEnvelope(message), Encoding.UTF8, "text/xml");
        // ICommonInterfaceMessageService declares [OperationContract(Action = "")] — the
        // dispatcher matches on the literal (quoted, empty) SOAPAction header, so it must be sent
        // explicitly; without it the request fails dispatch with ActionNotSupported.
        content.Headers.TryAddWithoutValidation("SOAPAction", "\"\"");

        using var response = await httpClient.PostAsync(BuildUri(infrastructureOperator.SystemUrl, "ci"), content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var responseXml = await response.Content.ReadAsStringAsync(cancellationToken);
        return string.Equals(ExtractResponseStatus(responseXml), "ACK", StringComparison.OrdinalIgnoreCase);
    }

    // POST /heartbeat — a pure reachability probe using the same UICHBMessage contract
    // TsiBroker.Im.Api itself hosts (see HeartbeatMessageService). Never throws: any failure to
    // connect or a non-2xx response both mean "not reachable".
    public async Task<bool> CheckHeartbeatAsync(
        InfrastructureOperator infrastructureOperator,
        CancellationToken cancellationToken = default)
    {
        using var content = new StringContent(BuildHeartbeatEnvelope(), Encoding.UTF8, "text/xml");
        content.Headers.TryAddWithoutValidation("SOAPAction", "\"\"");

        try
        {
            using var response = await httpClient.PostAsync(BuildUri(infrastructureOperator.SystemUrl, "heartbeat"), content, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    private static string BuildMessageEnvelope(BrokerMessage message)
    {
        var envelope = new XElement(
            Soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "s", Soap.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xsi", Xsi.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xsd", Xsd.NamespaceName),
            new XElement(
                Soap + "Header",
                new XElement(HeaderNs + "messageIdentifier", message.Id ?? Guid.NewGuid().ToString()),
                new XElement(HeaderNs + "messageLiHost", BrokerAlias)),
            new XElement(
                Soap + "Body",
                new XElement(
                    MessageNs + "UICMessage",
                    // Per the BDV Schnittstellenbeschreibung (2.7.1 "Nutzung WSDL"), `message` is
                    // declared as xs:anyType and carries the TSI message as a real nested XML
                    // element - not as an xsd:string-typed escaped blob.
                    new XElement("message", XElement.Parse(message.Content)),
                    new XElement("signature"),
                    new XElement("senderAlias", new XAttribute(Xsi + "type", "xsd:string"), BrokerAlias),
                    new XElement("encoding"))));

        return envelope.ToString(SaveOptions.DisableFormatting);
    }

    private static string BuildHeartbeatEnvelope()
    {
        var envelope = new XElement(
            Soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "s", Soap.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xsi", Xsi.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xsd", Xsd.NamespaceName),
            new XElement(
                Soap + "Body",
                new XElement(
                    MessageNs + "UICHBMessage",
                    new XElement("message", new XAttribute(Xsi + "type", "xsd:string"), BrokerAlias))));

        return envelope.ToString(SaveOptions.DisableFormatting);
    }

    private static string? ExtractResponseStatus(string responseXml)
    {
        try
        {
            var document = XDocument.Parse(responseXml);
            return document.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "ResponseStatus")
                ?.Value.Trim();
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    // Uri combining treats a base without a trailing slash as a "file", dropping its last
    // path segment (e.g. "https://im/api" + "ci" => "https://im/ci").
    private static Uri BuildUri(string systemUrl, string relativePath)
    {
        var baseUrl = systemUrl.EndsWith('/') ? systemUrl : systemUrl + "/";
        return new Uri(new Uri(baseUrl), relativePath);
    }
}
