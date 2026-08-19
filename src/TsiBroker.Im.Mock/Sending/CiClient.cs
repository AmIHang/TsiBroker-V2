using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Options;

namespace TsiBroker.Im.Mock.Sending;

/// <summary>
/// Client side of the Common Interface contract: builds the same SOAP envelope shape that
/// TsiBroker.Im.Api's CoreWCF-hosted /ci endpoint expects (see CommonInterfaceRequest /
/// TsiBroker.Im.Core.CI.Generated.UICMessage) and posts it there, playing the ISB sending a
/// message to the broker. Hand-built XML rather than a generated WCF client, since Im.Api's
/// binding builds/consumes the envelope declaratively and there was nothing to reuse here.
/// </summary>
public class CiClient(HttpClient httpClient, IOptions<CiClientOptions> options)
{
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace HeaderNs = "http://uic.cc.org/UICMessage/Header";
    private static readonly XNamespace MessageNs = "http://uic.cc.org/UICMessage";
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";
    private static readonly XNamespace Xsd = "http://www.w3.org/2001/XMLSchema";

    public async Task<CiSendResult> SendAsync(
        string payload,
        string? targetUrl = null,
        string? messageIdentifier = null,
        string? messageLiHost = null,
        string? senderAlias = null,
        CancellationToken cancellationToken = default)
    {
        var id = string.IsNullOrWhiteSpace(messageIdentifier) ? Guid.NewGuid().ToString() : messageIdentifier;
        var envelope = BuildEnvelope(
            payload,
            id,
            string.IsNullOrWhiteSpace(messageLiHost) ? "isb-mock" : messageLiHost,
            string.IsNullOrWhiteSpace(senderAlias) ? options.Value.SenderAlias : senderAlias);

        using var content = new StringContent(envelope, Encoding.UTF8, "text/xml");
        // ICommonInterfaceMessageService declares [OperationContract(Action = "")] - CoreWCF's
        // dispatcher matches on the literal (quoted, empty) SOAPAction header, so it must be sent
        // explicitly; without it the request fails dispatch with ActionNotSupported.
        content.Headers.TryAddWithoutValidation("SOAPAction", "\"\"");

        try
        {
            using var response = await httpClient.PostAsync(
                targetUrl ?? options.Value.TargetUrl,
                content,
                cancellationToken);
            var responseXml = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new CiSendResult(false, "HttpError", responseXml, $"HTTP {(int)response.StatusCode}");
            }

            var status = ExtractResponseStatus(responseXml) ?? "Unknown";
            return new CiSendResult(true, status, responseXml, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new CiSendResult(false, "Unreachable", null, ex.Message);
        }
    }

    private static string BuildEnvelope(string payload, string messageIdentifier, string messageLiHost, string senderAlias)
    {
        var envelope = new XElement(
            Soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "s", Soap.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xsi", Xsi.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xsd", Xsd.NamespaceName),
            new XElement(
                Soap + "Header",
                new XElement(HeaderNs + "messageIdentifier", messageIdentifier),
                new XElement(HeaderNs + "messageLiHost", messageLiHost),
                // Spec 4.1 "SOAP-Header": the BDV expects "false" for all three on every outbound
                // envelope.
                new XElement(HeaderNs + "compressed", false),
                new XElement(HeaderNs + "encrypted", false),
                new XElement(HeaderNs + "signed", false)),
            new XElement(
                Soap + "Body",
                new XElement(
                    MessageNs + "UICMessage",
                    // Per the BDV Schnittstellenbeschreibung (2.7.1 "Nutzung WSDL"), `message` is
                    // declared as xs:anyType and carries the TSI message as a real nested XML
                    // element - not as an xsd:string-typed escaped blob.
                    new XElement("message", XElement.Parse(payload)),
                    new XElement("signature"),
                    new XElement("senderAlias", new XAttribute(Xsi + "type", "xsd:string"), senderAlias),
                    new XElement("encoding"))));

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
}
