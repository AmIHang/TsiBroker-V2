using CoreWCF;
using System.Xml;
using Microsoft.Extensions.Logging;
using TsiBroker.Core.Messaging;
using TsiBroker.Core.RailwayUndertakings;
using TsiBroker.Im.Api.CI.Contracts;
using TsiBroker.Im.Core.CI;

namespace TsiBroker.Im.Api.CI;

[ServiceBehavior(IncludeExceptionDetailInFaults = true)]
public class CommonInterfaceMessageService(
    IMessagePublisher publisher,
    RailwayUndertakingStore railwayUndertakingStore,
    ILogger<CommonInterfaceMessageService> logger) : ICommonInterfaceMessageService
{
    public async Task<CommonInterfaceResponse> ReceiveAsync(CommonInterfaceRequest request)
    {
        XmlElement response;
        try
        {
            // The XmlSerializer-based UICMessage contract types `message` as `object`; whether it
            // deserializes as a string or an XmlElement depends on the sender's xsi:type
            // declaration (CiClient sends xsi:type="xsd:string", so it arrives as a plain string
            // containing the raw XML rather than as an XmlElement).
            var rawXml = request.Message?.message switch
            {
                string s => s,
                XmlElement el => el.OuterXml,
                _ => throw new ArgumentException("The SOAP body does not contain a message element.")
            };

            if (!IncomingTsiMessageParser.TryParse(rawXml, out var message, out var parseError))
            {
                throw new ArgumentException(parseError);
            }

            // The recipient RICS code identifies which EVU the message must be delivered to —
            // resolved here (rather than left to the outbound consumer) so an unknown/inactive
            // recipient is rejected immediately with a NACK instead of being queued.
            var railwayUndertaking = await railwayUndertakingStore.FindByRicsCodeAsync(message!.Recipient);
            if (railwayUndertaking is null || !railwayUndertaking.IsActive)
            {
                throw new ArgumentException(
                    $"No active railway undertaking found for recipient RICS code '{message.Recipient}'.");
            }

            var brokerMessage = new BrokerMessage(
                Id: message.MessageIdentifier,
                Sender: message.Sender,
                Receiver: message.Recipient,
                Content: rawXml,
                // One queue per EVU, prefixed to keep this direction's partitions distinct from
                // the ISB-keyed partitions used for the EVU->broker direction (see
                // EvuDeliveryCoordinator.PartitionKeyFor).
                PartitionKey: $"evu:{railwayUndertaking.Name}",
                MessageType: message.MessageType);

            await publisher.PublishAsync(brokerMessage);

            response = CreateResponse(request, ResponseStatus.ACK);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Rejecting inbound CI message {MessageIdentifier}", request.MessageIdentifier);
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
