using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;
using Xunit;

namespace TsiBroker.Im.Api.Tests;

// TICKET-1 coverage: /ci rejects a partner provisioned for 2-way SSL (PartnerCertificateBundle
// with ExpectedClientCaCertificateFileName set) when it presents no client certificate, accepts
// it once a certificate is presented (presence-only at this stage - CA/CN/CRL trust is TICKET-2),
// and leaves 1-way partners (today's only real-world case) unaffected.
public class ClientCertificateEnforcementTests
{
    private const string EvuRicsCode = "8010000-EVU";

    [Fact]
    public async Task Ci_TwoWayPartner_WithoutClientCertificate_IsRejected()
    {
        var serverCertificate = TestCertificates.CreateServerCertificate();
        await using var host = await ImApiTestHost.StartAsync(serverCertificate);
        await SeedRailwayUndertakingAsync(host);
        await SeedTwoWayInfrastructureOperatorAsync(host, "8010000-IM-2WAY-A");

        using var httpClient = CreateHttpClient(clientCertificate: null);
        using var response = await PostCiMessageAsync(httpClient, host.BaseAddress, "8010000-IM-2WAY-A");

        // The missing-certificate check throws a CoreWCF FaultException before the ACK/NACK
        // response body is built (see CommonInterfaceMessageService), so it surfaces as an
        // HTTP-level failure containing the fault reason, not a 200 OK NACK.
        Assert.False(response.IsSuccessStatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Client certificate required", body);
    }

    [Fact]
    public async Task Ci_TwoWayPartner_WithClientCertificate_IsAccepted()
    {
        var serverCertificate = TestCertificates.CreateServerCertificate();
        var clientCertificate = TestCertificates.CreateClientCertificate();
        await using var host = await ImApiTestHost.StartAsync(serverCertificate);
        await SeedRailwayUndertakingAsync(host);
        await SeedTwoWayInfrastructureOperatorAsync(host, "8010000-IM-2WAY-B");

        using var httpClient = CreateHttpClient(clientCertificate);
        using var response = await PostCiMessageAsync(httpClient, host.BaseAddress, "8010000-IM-2WAY-B");

        response.EnsureSuccessStatusCode();
        Assert.Equal("ACK", await ExtractResponseStatusAsync(response));
    }

    [Fact]
    public async Task Ci_OneWayPartner_WithoutClientCertificate_IsAccepted()
    {
        var serverCertificate = TestCertificates.CreateServerCertificate();
        await using var host = await ImApiTestHost.StartAsync(serverCertificate);
        await SeedRailwayUndertakingAsync(host);
        await host.InfrastructureOperators.AddAsync("One-way IM", "8010000-IM-1WAY", "https://im.example.test");

        using var httpClient = CreateHttpClient(clientCertificate: null);
        using var response = await PostCiMessageAsync(httpClient, host.BaseAddress, "8010000-IM-1WAY");

        response.EnsureSuccessStatusCode();
        Assert.Equal("ACK", await ExtractResponseStatusAsync(response));
    }

    private static Task SeedRailwayUndertakingAsync(ImApiTestHost host) =>
        host.RailwayUndertakings.AddAsync("Test EVU", [EvuRicsCode], "https://evu.example.test", []);

    private static async Task SeedTwoWayInfrastructureOperatorAsync(ImApiTestHost host, string ricsCode)
    {
        var infrastructureOperator = await host.InfrastructureOperators.AddAsync("Two-way IM", ricsCode, "https://im.example.test");
        var bundle = await host.CertificateBundles.GetOrCreateForOperatorAsync(infrastructureOperator.Id);
        await host.CertificateBundles.SaveExpectedClientCaCertificateAsync(bundle.Id, "dummy-ca-cert-bytes"u8.ToArray());
    }

    private static HttpClient CreateHttpClient(X509Certificate2? clientCertificate)
    {
        var handler = new SocketsHttpHandler
        {
            SslOptions = { RemoteCertificateValidationCallback = (_, _, _, _) => true },
        };
        if (clientCertificate is not null)
        {
            handler.SslOptions.ClientCertificates = new X509CertificateCollection { clientCertificate };
        }

        return new HttpClient(handler);
    }

    private static Task<HttpResponseMessage> PostCiMessageAsync(HttpClient httpClient, Uri baseAddress, string senderRicsCode)
    {
        var envelope = CiEnvelope.Build(senderRicsCode, EvuRicsCode, Guid.NewGuid().ToString());
        var content = new StringContent(envelope, Encoding.UTF8, "text/xml");
        content.Headers.TryAddWithoutValidation("SOAPAction", "\"\"");
        return httpClient.PostAsync(new Uri(baseAddress, "/ci"), content);
    }

    private static async Task<string?> ExtractResponseStatusAsync(HttpResponseMessage response)
    {
        var xml = await response.Content.ReadAsStringAsync();
        var document = XDocument.Parse(xml);
        return document.Descendants().FirstOrDefault(e => e.Name.LocalName == "ResponseStatus")?.Value.Trim();
    }
}
