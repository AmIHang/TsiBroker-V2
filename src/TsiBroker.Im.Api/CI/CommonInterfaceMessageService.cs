using CoreWCF;
using System.Xml;
using TsiBroker.Im.Api.CI.Contracts;
using TsiBroker.Im.Api.Messaging;

namespace TsiBroker.Im.Api.CI;

[ServiceBehavior(IncludeExceptionDetailInFaults = true)]
public class CommonInterfaceMessageService(IMessagePublisher publisher) : ICommonInterfaceMessageService
{
    public async Task<CommonInterfaceResponse> ReceiveAsync(CommonInterfaceRequest request)
    {
        XmlElement response;
        try
        {
            if (request.Message is null)
            {
                throw new ArgumentException(
                    "The SOAP body does not contain a message element.");
            }

            var brokerMessage = new BrokerMessage(
                Id: request.MessageIdentifier,
                Sender: "CI",
                Receiver: "App",
                Content: request.Message.message?.ToString() ?? "NULL");

            await publisher.PublishAsync(brokerMessage);

            response = CreateResponse(request, ResponseStatus.ACK);
        }
        catch
        {
            response = CreateResponse(request, ResponseStatus.NACK);
        }

        return new CommonInterfaceResponse
        {
            Return = response
        };
    }

    private enum ResponseStatus
    {
        ACK,
        NACK
    }

    private static XmlElement CreateResponse(CommonInterfaceRequest request, ResponseStatus status)
    {
        return TechnicalAckFactory.Create(
            responseStatus: status.ToString(),
            ackIdentifier: Guid.NewGuid().ToString(),
            messageReference: request.MessageIdentifier ?? string.Empty,
            sender: "broker",
            recipient: request.MessageLiHost ?? string.Empty,
            remoteLiName: request.MessageLiHost ?? string.Empty,
            remoteLiInstanceNumber: string.Empty,
            messageTransportMechanism: "SOAP");
    }
}
