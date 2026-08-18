using System.Xml.Linq;

namespace TsiBroker.Im.Api.Tests;

// Minimal reimplementation of the SOAP envelope shape TsiBroker.Im.Mock's CiClient builds for
// TsiBroker.Im.Api's /ci endpoint (see TsiBroker.Im.Mock/Sending/CiClient.cs). Kept local instead
// of referencing TsiBroker.Im.Mock, which pulls in RabbitMQ/consumer wiring this test suite
// doesn't need.
internal static class CiEnvelope
{
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace HeaderNs = "http://uic.cc.org/UICMessage/Header";
    private static readonly XNamespace MessageNs = "http://uic.cc.org/UICMessage";
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";
    private static readonly XNamespace Xsd = "http://www.w3.org/2001/XMLSchema";

    public static string Build(string senderRicsCode, string recipientRicsCode, string messageIdentifier)
    {
        var payload = $"""
            <TrainRunningInformationMessage>
              <MessageHeader>
                <MessageReference>
                  <MessageType>TrainRunningInformationMessage</MessageType>
                  <MessageIdentifier>{messageIdentifier}</MessageIdentifier>
                </MessageReference>
                <Sender>{senderRicsCode}</Sender>
                <Recipient>{recipientRicsCode}</Recipient>
              </MessageHeader>
              <TrainRunningInformation>
                <Timestamp>2026-08-07T10:00:00Z</Timestamp>
              </TrainRunningInformation>
            </TrainRunningInformationMessage>
            """;

        var envelope = new XElement(
            Soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "s", Soap.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xsi", Xsi.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xsd", Xsd.NamespaceName),
            new XElement(
                Soap + "Header",
                new XElement(HeaderNs + "messageIdentifier", messageIdentifier),
                new XElement(HeaderNs + "messageLiHost", "test-li-host")),
            new XElement(
                Soap + "Body",
                new XElement(
                    MessageNs + "UICMessage",
                    new XElement("message", XElement.Parse(payload)),
                    new XElement("signature"),
                    new XElement("senderAlias", new XAttribute(Xsi + "type", "xsd:string"), senderRicsCode),
                    new XElement("encoding"))));

        return envelope.ToString(SaveOptions.DisableFormatting);
    }
}
