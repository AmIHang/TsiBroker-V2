using CoreWCF;
using System.Xml;

namespace TsiBroker.Im.Api.Heartbeat.Contracts;

[MessageContract(
    IsWrapped = true,
    WrapperName = "UICHBMessageResponse",
    WrapperNamespace = "http://uic.cc.org/UICMessage")]
public class HeartbeatResponse
{
    [MessageBodyMember(
        Name = "return",
        Namespace = "",
        Order = 0)]
    public XmlElement[] Return { get; set; } = [];
}
