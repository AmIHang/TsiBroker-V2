using System.Xml;
using System.Xml.Linq;

namespace TsiBroker.Im.Mock.Receiving;

/// <summary>
/// Best-effort, namespace-agnostic lookup of a MessageIdentifier element anywhere in the
/// document - used only to give received/sent messages a human-friendly log entry, not for
/// validation (unlike TsiBroker.Ru.Api's stricter MessageHeader parsing).
/// </summary>
internal static class MessageIdentifierParser
{
    public static string? TryExtract(string rawXml)
    {
        try
        {
            var document = XDocument.Parse(rawXml);
            return document.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "MessageIdentifier")
                ?.Value.Trim();
        }
        catch (XmlException)
        {
            return null;
        }
    }
}
