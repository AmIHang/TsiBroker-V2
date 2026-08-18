using CoreWCF;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TsiBroker.Core.Certificates;
using TsiBroker.Core.InfrastructureOperators;
using TsiBroker.Core.Messaging;
using TsiBroker.Core.RailwayUndertakings;
using TsiBroker.Im.Api.CI.Contracts;
using TsiBroker.Im.Core.CI;

namespace TsiBroker.Im.Api.CI;

[ServiceBehavior(IncludeExceptionDetailInFaults = true)]
public class CommonInterfaceMessageService(
    IMessagePublisher publisher,
    RailwayUndertakingStore railwayUndertakingStore,
    InfrastructureOperatorStore infrastructureOperatorStore,
    PartnerCertificateProvider certificateProvider,
    PartnerCertificateValidator certificateValidator,
    IHttpContextAccessor httpContextAccessor,
    ILogger<CommonInterfaceMessageService> logger) : ICommonInterfaceMessageService
{
    public async Task<CommonInterfaceResponse> ReceiveAsync(CommonInterfaceRequest request)
    {
        // The XmlSerializer-based UICMessage contract types `message` as `object`; its actual
        // runtime shape depends on how the sender wrote the element. Per the BDV
        // Schnittstellenbeschreibung, `message` is xs:anyType carrying the TSI message as a real
        // nested XML element (no xsi:type) — XmlSerializer deserializes that shape as
        // System.Xml.XmlNode[] (a single-element array wrapping the child element), not as
        // XmlElement directly. The xsi:type="xsd:string"/XmlElement cases are kept for senders
        // that use those shapes instead.
        var rawXml = request.Message?.message switch
        {
            string s => s,
            XmlElement el => el.OuterXml,
            XmlNode[] nodes => nodes.OfType<XmlElement>().FirstOrDefault()?.OuterXml,
            _ => null
        };
        string? parseError = null;
        var parsed = rawXml is not null && IncomingTsiMessageParser.TryParse(rawXml, out var parsedMessage, out parseError)
            ? parsedMessage
            : null;

        // The client-certificate check deliberately sits outside the try/catch below: a
        // missing/malformed message is a business-level rejection (NACK, HTTP 200), but a missing
        // certificate for a partner provisioned for 2-way SSL is a connection-level rejection
        // (SOAP fault, HTTP 500) — the two must stay distinguishable in the response, so a cert
        // failure can't be swallowed into an ordinary NACK.
        if (parsed is not null)
        {
            var callingOperator = await infrastructureOperatorStore.FindByRicsCodeAsync(parsed.Sender);
            if (callingOperator is not null)
            {
                var bundle = await certificateProvider.GetBundleAsync(callingOperator.Id);
                if (bundle?.RequiresClientCertificate == true)
                {
                    var httpContext = httpContextAccessor.HttpContext
                        ?? throw new InvalidOperationException("No HttpContext available for the current CoreWCF request.");
                    var clientCertificate = await httpContext.Connection.GetClientCertificateAsync();
                    if (clientCertificate is null)
                    {
                        // .NET/CoreWCF functional equivalent of the spec's SSLHandshakeException:
                        // "Received fatal alert: bad_certificate" — surfaced one layer up (SOAP
                        // fault) rather than at the raw TLS handshake, because this Kestrel
                        // endpoint uses ClientCertificateMode.AllowCertificate (not
                        // RequireCertificate) so that 1-way partners can keep sharing the same
                        // port.
                        logger.LogWarning(
                            "Rejecting inbound CI message from {Sender}: partner is provisioned for 2-way SSL but no client certificate was presented",
                            parsed.Sender);
                        throw new FaultException("Client certificate required.");
                    }

                    // Spec 4.3's CA/CN/CRL check on the presented client certificate — against the
                    // partner's own configured CA (PartnerCertificateBundle), not the machine trust
                    // store, since Kestrel already accepted the handshake without validating the
                    // chain at all (AllowAnyClientCertificate, see Program.cs).
                    var expectedCaCertificate = await certificateProvider.GetExpectedClientCaCertificateAsync(callingOperator.Id);
                    var validationResult = await certificateValidator.ValidateClientCertificateAsync(bundle, clientCertificate, expectedCaCertificate);
                    if (!validationResult.IsValid)
                    {
                        logger.LogWarning(
                            "Rejecting inbound CI message from {Sender}: client certificate failed validation ({FailureReason})",
                            parsed.Sender,
                            validationResult.FailureReason);
                        throw new FaultException($"Client certificate validation failed: {validationResult.FailureReason}.");
                    }
                }
            }
        }

        XmlElement response;
        try
        {
            if (rawXml is null)
            {
                throw new ArgumentException("The SOAP body does not contain a message element.");
            }

            var message = parsed ?? throw new ArgumentException(parseError);

            // The recipient RICS code identifies which EVU the message must be delivered to —
            // resolved here (rather than left to the outbound consumer) so an unknown/inactive
            // recipient is rejected immediately with a NACK instead of being queued.
            var railwayUndertaking = await railwayUndertakingStore.FindByRicsCodeAsync(message.Recipient);
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
