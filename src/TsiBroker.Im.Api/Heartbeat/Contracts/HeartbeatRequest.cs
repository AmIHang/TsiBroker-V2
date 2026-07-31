using CoreWCF;
using System.Xml;

namespace TsiBroker.Im.Api.Heartbeat.Contracts;

[MessageContract(
    IsWrapped = true,
    WrapperName = "UICHBMessage",
    WrapperNamespace = "http://uic.cc.org/UICMessage")]
public class HeartbeatRequest
{
    [MessageBodyMember(
        Name = "message",
        Namespace = "",
        Order = 0)]
    public XmlElement? Message { get; set; }

    [MessageBodyMember(
        Name = "properties",
        Namespace = "",
        Order = 1)]
    public XmlElement? Properties { get; set; }
}
