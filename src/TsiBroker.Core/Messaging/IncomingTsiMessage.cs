using System.Xml.Linq;

namespace TsiBroker.Core.Messaging;

// Mirrors the TAF/TAP TSI MessageHeader shape (Sender/Recipient RICS codes, MessageReference/MessageType)
// that XSD-generated TSI documents already carry as the first child of the message root element.
// Namespace-agnostic and direction-agnostic — used both for EVU->broker (TsiBroker.Ru.Api) and
// IM->broker (TsiBroker.Im.Api) message ingestion.
public record IncomingTsiMessage(
    string MessageType,
    string Sender,
    string Recipient,
    string? MessageIdentifier,
    string RawXml);

public static class IncomingTsiMessageParser
{
    public static bool TryParse(string rawXml, out IncomingTsiMessage? message, out string? error)
    {
        message = null;

        if (string.IsNullOrWhiteSpace(rawXml))
        {
            error = "Request body must contain a TAF/TAP TSI message XML document.";
            return false;
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(rawXml);
        }
        catch (Exception ex)
        {
            error = $"Request body is not well-formed XML: {ex.Message}";
            return false;
        }

        var root = document.Root;
        var messageHeader = root?.Elements().FirstOrDefault(e => e.Name.LocalName == "MessageHeader");
        if (messageHeader is null)
        {
            error = "Message is missing the required MessageHeader element.";
            return false;
        }

        var messageReference = messageHeader.Elements().FirstOrDefault(e => e.Name.LocalName == "MessageReference");
        var messageType = messageReference?.Elements().FirstOrDefault(e => e.Name.LocalName == "MessageType")?.Value.Trim();
        var messageIdentifier = messageReference?.Elements().FirstOrDefault(e => e.Name.LocalName == "MessageIdentifier")?.Value.Trim();
        var sender = messageHeader.Elements().FirstOrDefault(e => e.Name.LocalName == "Sender")?.Value.Trim();
        var recipient = messageHeader.Elements().FirstOrDefault(e => e.Name.LocalName == "Recipient")?.Value.Trim();

        if (string.IsNullOrEmpty(messageType) || string.IsNullOrEmpty(sender) || string.IsNullOrEmpty(recipient))
        {
            error = "MessageHeader must contain MessageReference/MessageType, Sender and Recipient.";
            return false;
        }

        message = new IncomingTsiMessage(messageType, sender, recipient, messageIdentifier, rawXml);
        error = null;
        return true;
    }
}
