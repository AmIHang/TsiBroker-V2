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

    [XmlArray("InfrastructureOperators")]
    [XmlArrayItem("InfrastructureOperator")]
    public List<WhoAmIInfrastructureOperator> InfrastructureOperators { get; set; } = [];
}

public class WhoAmIInfrastructureOperator
{
    [XmlElement("Name")]
    public string Name { get; set; } = string.Empty;

    [XmlElement("RicsCode")]
    public string RicsCode { get; set; } = string.Empty;

    [XmlArray("AllowedMessageTypesEvuToBroker")]
    [XmlArrayItem("MessageType")]
    public List<string> AllowedMessageTypesEvuToBroker { get; set; } = [];

    [XmlArray("AllowedMessageTypesBrokerToEvu")]
    [XmlArrayItem("MessageType")]
    public List<string> AllowedMessageTypesBrokerToEvu { get; set; } = [];
}
