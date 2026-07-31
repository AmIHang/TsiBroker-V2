using CoreWCF;
using TsiBroker.Im.Api.Heartbeat.Contracts;

namespace TsiBroker.Im.Api.Heartbeat;

[ServiceContract(
    Name = "UICHBMessage",
    Namespace = "http://uic.cc.org/UICMessage")]
public interface IHeartbeatMessageService
{
    [OperationContract(
        Name = "UICHBMessage",
        Action = "",
        ReplyAction = "*")]
    Task<HeartbeatResponse> ReceiveAsync(HeartbeatRequest request);
}
