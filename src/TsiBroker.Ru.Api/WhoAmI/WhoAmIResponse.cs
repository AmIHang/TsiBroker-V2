using System.Xml.Serialization;

namespace TsiBroker.Ru.Api.WhoAmI;

[XmlRoot("RailwayUndertaking")]
public class WhoAmIResponse
{
    [XmlElement("Name")]
    public string Name { get; set; } = string.Empty;

    [XmlArray("RicsCodes")]
    [XmlArrayItem("RicsCode")]
    public List<string> RicsCodes { get; set; } = [];

    [XmlArray("Permissions")]
    [XmlArrayItem("Permission")]
    public List<WhoAmIPermission> Permissions { get; set; } = [];
}

public class WhoAmIPermission
{
    [XmlAttribute("Sender")]
    public string Sender { get; set; } = string.Empty;

    [XmlAttribute("Receiver")]
    public string Receiver { get; set; } = string.Empty;

    [XmlAttribute("MessageType")]
    public string MessageType { get; set; } = string.Empty;
}
