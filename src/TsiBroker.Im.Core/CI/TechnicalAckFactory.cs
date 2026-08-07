using System.Xml;

namespace TsiBroker.Im.Core.CI;

/// <summary>
/// Builds the LI_TechnicalAck response used by both sides of the Common Interface contract:
/// TsiBroker.Im.Api replies with this to real Infrastructure Managers, and TsiBroker.Im.Mock
/// replies with this when standing in for an Infrastructure Manager during tests.
/// </summary>
public static class TechnicalAckFactory
{
    public static XmlElement Create(
        string responseStatus,
        string ackIdentifier,
        string messageReference,
        string sender,
        string recipient,
        string remoteLiName,
        string remoteLiInstanceNumber,
        string messageTransportMechanism)
    {
        if (responseStatus is not ("ACK" or "NACK"))
        {
            throw new ArgumentOutOfRangeException(
                nameof(responseStatus),
                "ResponseStatus must be ACK or NACK.");
        }

        var document = new XmlDocument();

        var root = document.CreateElement("LI_TechnicalAck");
        document.AppendChild(root);

        AppendElement(document, root, "ResponseStatus", responseStatus);

        // Der Schreibfehler stammt aus dem gelieferten Schema.
        AppendElement(document, root, "AckIndentifier", ackIdentifier);

        AppendElement(document, root, "MessageReference", messageReference);
        AppendElement(document, root, "Sender", sender);
        AppendElement(document, root, "Recipient", recipient);
        AppendElement(document, root, "RemoteLIName", remoteLiName);
        AppendElement(
            document,
            root,
            "RemoteLIInstanceNumber",
            remoteLiInstanceNumber);

        AppendElement(
            document,
            root,
            "MessageTransportMechanism",
            messageTransportMechanism);

        return root;
    }

    private static void AppendElement(
        XmlDocument document,
        XmlElement parent,
        string name,
        string value)
    {
        var element = document.CreateElement(name);
        element.InnerText = value;
        parent.AppendChild(element);
    }
}
