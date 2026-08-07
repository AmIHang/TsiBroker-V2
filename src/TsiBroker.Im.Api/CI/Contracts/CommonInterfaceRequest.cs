using CoreWCF;
using TsiBroker.Im.Core.CI.Generated;

namespace TsiBroker.Im.Api.CI.Contracts;

[MessageContract(IsWrapped = false)]
public sealed class CommonInterfaceRequest
{
    [MessageHeader(
        Name = "messageIdentifier",
        Namespace = "http://uic.cc.org/UICMessage/Header")]
    public string? MessageIdentifier { get; set; }

    [MessageHeader(
        Name = "messageLiHost",
        Namespace = "http://uic.cc.org/UICMessage/Header")]
    public string? MessageLiHost { get; set; }

    [MessageHeader(
        Name = "compressed",
        Namespace = "http://uic.cc.org/UICMessage/Header")]
    public bool? Compressed { get; set; }

    [MessageHeader(
        Name = "encrypted",
        Namespace = "http://uic.cc.org/UICMessage/Header")]
    public bool? Encrypted { get; set; }

    [MessageHeader(
        Name = "signed",
        Namespace = "http://uic.cc.org/UICMessage/Header")]
    public bool? Signed { get; set; }

    [MessageBodyMember(
        Name = "UICMessage",
        Namespace = "http://uic.cc.org/UICMessage",
        Order = 0)]
    public UICMessage? Message { get; set; }
}
