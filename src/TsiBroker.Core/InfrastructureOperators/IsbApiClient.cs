using System.Net.Http;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TsiBroker.Core.Certificates;
using TsiBroker.Core.Messaging;

namespace TsiBroker.Core.InfrastructureOperators;

// Spec 2.3.2 step 2 distinguishes four outcomes of a delivery POST, not just success/failure —
// InfrastructureOperatorConsumerCoordinator treats each differently (see its HandleMessageAsync).
public enum DeliveryOutcome
{
    // Step 2a: ACK — the IM processed the message successfully.
    Acknowledged,
    // Step 2a: NACK — also a completed send as far as transport is concerned; the spec treats
    // this as "successfully abgeschlossen" and only logs it for analysis, unlike a transport
    // failure or timeout.
    NegativeAcknowledged,
    // Step 2b: a response was received but it's neither ACK nor NACK (or not HTTP-successful) —
    // the spec says to close the connection and discard the message outright, not retry it.
    InvalidResponse,
    // Step 2c: no response within DeliveryTimeout — resend until the message's max age is
    // reached.
    TimedOut,
    // Connection/TLS failure before any response was received (e.g. the IM is unreachable). Not
    // explicitly covered by spec 2.3.2, which only discusses step 1's CA/CRL handshake failure
    // (discarded outright) — but a general connectivity failure is exactly what
    // InfrastructureOperatorReachabilityMonitor's pause/resume mechanism exists to handle, so the
    // coordinator treats this the same as TimedOut (checks reachability, then resends until max
    // age) rather than discarding immediately.
    TransportFailure,
}

// Outbound SOAP calls from the broker to an Infrastrukturbetreiber's own system
// (InfrastructureOperator.SystemUrl) — the mirror image of the inbound IM -> broker SOAP contract
// TsiBroker.Im.Api itself hosts (ICommonInterfaceMessageService / IHeartbeatMessageService).
// Hand-built XML rather than a generated WCF client, the same way TsiBroker.Im.Mock's CiClient
// builds its own UICMessage envelope — there's no partner-supplied WSDL for a generic "any IM" to
// generate a client from.
//
// Spec 4.3: SOAP delivery always uses 2-way SSL, and each IM partner can have its own client
// certificate for the broker to present (TICKET-3, mirroring TICKET-2's inbound direction), plus a
// CA/CRL check against the server certificate the IM presents back once one is configured for that
// partner (TICKET-4, spec 2.3.2 step 1). A single shared HttpClient can't do either —
// HttpClientHandler.ClientCertificates and
// .ServerCertificateCustomValidationCallback are fixed per handler/connection, not selectable per
// outbound call, and IHttpClientFactory's named/typed clients are wired up once at startup while
// partners are created and their certificates rotated at runtime (PartnerCertificateProvider). So
// this client resolves and attaches the right certificate/validation per call instead of once via
// DI — see CreateHttpClientAsync.
public class IsbApiClient(
    PartnerCertificateProvider certificateProvider,
    PartnerCertificateValidator certificateValidator,
    CrlCache crlCache,
    IOptions<InfrastructureOperatorDeliveryOptions> deliveryOptions,
    ILogger<IsbApiClient> logger)
{
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace HeaderNs = "http://uic.cc.org/UICMessage/Header";
    private static readonly XNamespace MessageNs = "http://uic.cc.org/UICMessage";
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";
    private static readonly XNamespace Xsd = "http://www.w3.org/2001/XMLSchema";

    private const string BrokerAlias = "broker";

    // Matches the timeout AddHttpClient<EvuApiClient> configures via Program.cs — kept here
    // instead since this client no longer goes through DI's typed-client registration (see class
    // comment). Short so an unreachable IM is detected in seconds, not the 100s HttpClient
    // default — InfrastructureOperatorReachabilityMonitor/PollAsync blocks on this call.
    private static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(10);

    // POST /ci — delivers a TSI message to the IM's Common Interface endpoint, wrapped in the
    // same UICMessage SOAP envelope TsiBroker.Im.Api itself expects (see
    // CommonInterfaceMessageService.ReceiveAsync). Classifies the outcome per spec 2.3.2 step 2 —
    // see DeliveryOutcome for what each value means and how
    // InfrastructureOperatorConsumerCoordinator reacts to it.
    public async Task<DeliveryOutcome> DeliverMessageAsync(
        InfrastructureOperator infrastructureOperator,
        BrokerMessage message,
        CancellationToken cancellationToken = default)
    {
        using var content = new StringContent(BuildMessageEnvelope(message), Encoding.UTF8, "text/xml");
        // ICommonInterfaceMessageService declares [OperationContract(Action = "")] — the
        // dispatcher matches on the literal (quoted, empty) SOAPAction header, so it must be sent
        // explicitly; without it the request fails dispatch with ActionNotSupported.
        content.Headers.TryAddWithoutValidation("SOAPAction", "\"\"");

        using var httpClient = await CreateHttpClientAsync(infrastructureOperator, deliveryOptions.Value.DeliveryTimeout);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsync(BuildUri(infrastructureOperator.SystemUrl, "ci"), content, cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout (DeliveryTimeout) elapsed without a response — spec 2.3.2 step
            // 2c, not a real cancellation (that would have cancellationToken already signalled).
            return DeliveryOutcome.TimedOut;
        }
        catch (HttpRequestException)
        {
            return DeliveryOutcome.TransportFailure;
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return DeliveryOutcome.InvalidResponse;
            }

            var responseXml = await response.Content.ReadAsStringAsync(cancellationToken);
            var technicalAck = ExtractTechnicalAck(responseXml);
            switch (technicalAck.ResponseStatus?.ToUpperInvariant())
            {
                case "ACK":
                    return DeliveryOutcome.Acknowledged;
                case "NACK":
                    // Spec 2.3.2 step 2a: a NACK is still a completed send — only logged here for
                    // analysis, not retried (see InfrastructureOperatorConsumerCoordinator).
                    logger.LogWarning(
                        "IM {InfrastructureOperatorName} returned NACK for message {MessageId} (AckIdentifier={AckIdentifier}, MessageReference={MessageReference})",
                        infrastructureOperator.Name,
                        message.Id,
                        technicalAck.AckIdentifier,
                        technicalAck.MessageReference);
                    return DeliveryOutcome.NegativeAcknowledged;
                default:
                    // Spec 2.3.2 step 2b: any other (or unparseable) response — discard, don't retry.
                    return DeliveryOutcome.InvalidResponse;
            }
        }
    }

    // POST /heartbeat — a pure reachability probe using the same UICHBMessage contract
    // TsiBroker.Im.Api itself hosts (see HeartbeatMessageService). Never throws: any failure to
    // connect or a non-2xx response both mean "not reachable".
    public async Task<bool> CheckHeartbeatAsync(
        InfrastructureOperator infrastructureOperator,
        CancellationToken cancellationToken = default)
    {
        using var content = new StringContent(BuildHeartbeatEnvelope(), Encoding.UTF8, "text/xml");
        content.Headers.TryAddWithoutValidation("SOAPAction", "\"\"");

        try
        {
            using var httpClient = await CreateHttpClientAsync(infrastructureOperator, HeartbeatTimeout);
            using var response = await httpClient.PostAsync(BuildUri(infrastructureOperator.SystemUrl, "heartbeat"), content, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    // Resolves this IM partner's client certificate (if any — spec 4.3 requires 2-way SSL for
    // every partner, but not every partner is provisioned yet) and the CA/CRL used to validate the
    // partner's own server certificate during the handshake, then builds a handler/client scoped to
    // just this call. PartnerCertificateProvider re-reads from disk on every call, so a certificate
    // uploaded or rotated via the admin API is picked up on the very next delivery attempt, with no
    // cache to invalidate and no process restart needed.
    private async Task<HttpClient> CreateHttpClientAsync(InfrastructureOperator infrastructureOperator, TimeSpan timeout)
    {
        var handler = new HttpClientHandler();
        var clientCertificate = await certificateProvider.GetClientCertificateAsync(infrastructureOperator.Id);
        if (clientCertificate is not null)
        {
            handler.ClientCertificates.Add(clientCertificate);
        }

        // TICKET-4 (spec 2.3.2 step 1): validate the IM's server certificate against this partner's
        // configured CA/CRL. Same "not every partner is provisioned yet" reasoning as the client
        // certificate above — a partner with no expected server CA configured has this check
        // skipped entirely (no validation at all, not even the OS default trust-store check),
        // rather than failing every call to a partner nobody has gotten around to provisioning yet.
        var bundle = await certificateProvider.GetBundleAsync(infrastructureOperator.Id);
        var expectedServerCaCertificate = await certificateProvider.GetExpectedServerCaCertificateAsync(infrastructureOperator.Id);
        if (expectedServerCaCertificate is null)
        {
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        }
        else
        {
            // The CRL fetch has to happen up front, before the TLS handshake starts:
            // ServerCertificateCustomValidationCallback below is a synchronous hook, so it can't
            // await CrlCache itself.
            var serverCrl = !string.IsNullOrWhiteSpace(bundle?.ServerCrlUrl)
                ? await crlCache.GetAsync(bundle.ServerCrlUrl)
                : null;

            handler.ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
            {
                if (certificate is null)
                {
                    return false;
                }

                var result = certificateValidator.ValidateServerCertificate(bundle, certificate, expectedServerCaCertificate, serverCrl);
                if (!result.IsValid)
                {
                    logger.LogWarning(
                        "Closing outbound connection to {InfrastructureOperatorName}: server certificate failed validation ({FailureReason})",
                        infrastructureOperator.Name,
                        result.FailureReason);
                }

                return result.IsValid;
            };
        }

        return new HttpClient(handler) { Timeout = timeout };
    }

    private static string BuildMessageEnvelope(BrokerMessage message)
    {
        var envelope = new XElement(
            Soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "s", Soap.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xsi", Xsi.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xsd", Xsd.NamespaceName),
            new XElement(
                Soap + "Header",
                new XElement(HeaderNs + "messageIdentifier", message.Id ?? Guid.NewGuid().ToString()),
                new XElement(HeaderNs + "messageLiHost", BrokerAlias)),
            new XElement(
                Soap + "Body",
                new XElement(
                    MessageNs + "UICMessage",
                    // Per the BDV Schnittstellenbeschreibung (2.7.1 "Nutzung WSDL"), `message` is
                    // declared as xs:anyType and carries the TSI message as a real nested XML
                    // element - not as an xsd:string-typed escaped blob.
                    new XElement("message", XElement.Parse(message.Content)),
                    new XElement("signature"),
                    new XElement("senderAlias", new XAttribute(Xsi + "type", "xsd:string"), BrokerAlias),
                    new XElement("encoding"))));

        return envelope.ToString(SaveOptions.DisableFormatting);
    }

    private static string BuildHeartbeatEnvelope()
    {
        var envelope = new XElement(
            Soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "s", Soap.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xsi", Xsi.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xsd", Xsd.NamespaceName),
            new XElement(
                Soap + "Body",
                new XElement(
                    MessageNs + "UICHBMessage",
                    new XElement("message", new XAttribute(Xsi + "type", "xsd:string"), BrokerAlias))));

        return envelope.ToString(SaveOptions.DisableFormatting);
    }

    // AckIdentifier/MessageReference are only used for the NACK analysis log above, so a failed
    // parse just yields nulls (=> InvalidResponse) rather than a separate error path.
    private static TechnicalAck ExtractTechnicalAck(string responseXml)
    {
        try
        {
            var elements = XDocument.Parse(responseXml).Descendants().ToList();
            return new TechnicalAck(
                elements.FirstOrDefault(e => e.Name.LocalName == "ResponseStatus")?.Value.Trim(),
                // Sic: "AckIndentifier" is a typo carried over from the partner-supplied schema
                // (see TechnicalAckFactory.Create), not a mistake here.
                elements.FirstOrDefault(e => e.Name.LocalName == "AckIndentifier")?.Value.Trim(),
                elements.FirstOrDefault(e => e.Name.LocalName == "MessageReference")?.Value.Trim());
        }
        catch (System.Xml.XmlException)
        {
            return new TechnicalAck(null, null, null);
        }
    }

    private readonly record struct TechnicalAck(string? ResponseStatus, string? AckIdentifier, string? MessageReference);

    // Uri combining treats a base without a trailing slash as a "file", dropping its last
    // path segment (e.g. "https://im/api" + "ci" => "https://im/ci").
    private static Uri BuildUri(string systemUrl, string relativePath)
    {
        var baseUrl = systemUrl.EndsWith('/') ? systemUrl : systemUrl + "/";
        return new Uri(new Uri(baseUrl), relativePath);
    }
}
