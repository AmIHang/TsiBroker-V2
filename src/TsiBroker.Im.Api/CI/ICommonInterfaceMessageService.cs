using CoreWCF;
using TsiBroker.Im.Api.CI.Contracts;

namespace TsiBroker.Im.Api.CI;

[ServiceContract(
    Name = "UICReceiveMessage",
    Namespace = "http://uic.cc.org/UICMessage")]
public interface ICommonInterfaceMessageService
{
    [OperationContract(
        Name = "UICMessage",
        Action = "",
        ReplyAction = "*")]
    [System.ServiceModel.XmlSerializerFormat]
    Task<CommonInterfaceResponse> ReceiveAsync(CommonInterfaceRequest request);
}
