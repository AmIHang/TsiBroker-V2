using CoreWCF;
using System.Xml;

namespace TsiBroker.Im.Api.CI.Contracts;

[MessageContract(
    IsWrapped = true,
    WrapperName = "UICMessageResponse",
    WrapperNamespace = "http://uic.cc.org/UICMessage")]
public sealed class CommonInterfaceResponse
{
    [MessageBodyMember(
        Name = "return",
        Namespace = "",
        Order = 0)]
    public XmlElement? Return { get; set; }
}
