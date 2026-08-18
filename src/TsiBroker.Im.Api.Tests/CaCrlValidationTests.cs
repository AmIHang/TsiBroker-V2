using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace TsiBroker.Im.Api.Tests;

// TICKET-2 coverage: /ci runs PartnerCertificateValidator.ValidateClientCertificateAsync against a
// presented client certificate once TICKET-1 has confirmed one was presented at all — the three
// cases required by the ticket's acceptance criteria: an untrusted CA, a certificate on the
// configured CRL, and a valid certificate from the configured CA that is not on the CRL.
public class CaCrlValidationTests
{
    private const string EvuRicsCode = "8010000-EVU";

    [Fact]
    public async Task Ci_ClientCertificateFromUntrustedCa_IsRejected()
    {
        var serverCertificate = TestCertificates.CreateServerCertificate();
        var expectedCa = CaCrlTestCertificates.CreateCa("Expected Partner CA");
        var untrustedCa = CaCrlTestCertificates.CreateCa("Untrusted CA");
        var clientCertificate = CaCrlTestCertificates.CreateClientCertificateSignedBy(
            untrustedCa, "test-partner", out _);

        await using var host = await ImApiTestHost.StartAsync(serverCertificate);
        await SeedRailwayUndertakingAsync(host);
        await SeedTwoWayOperatorAsync(host, "8010000-IM-UNTRUSTED-CA", expectedCa, clientCrlUrl: null);

        using var httpClient = CiTestHelpers.CreateHttpClient(clientCertificate);
        using var response = await CiTestHelpers.PostCiMessageAsync(httpClient, host.BaseAddress, "8010000-IM-UNTRUSTED-CA", EvuRicsCode);

        Assert.False(response.IsSuccessStatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ChainNotTrusted", body);
    }

    [Fact]
    public async Task Ci_RevokedClientCertificate_IsRejected()
    {
        var serverCertificate = TestCertificates.CreateServerCertificate();
        var ca = CaCrlTestCertificates.CreateCa();

        await using var crlServer = await CrlHttpServer.StartAsync();
        var clientCertificate = CaCrlTestCertificates.CreateClientCertificateSignedBy(
            ca, "test-partner", out var serialNumber);
        crlServer.SetCrl(CaCrlTestCertificates.BuildCrl(ca, serialNumber));

        await using var host = await ImApiTestHost.StartAsync(serverCertificate);
        await SeedRailwayUndertakingAsync(host);
        await SeedTwoWayOperatorAsync(host, "8010000-IM-REVOKED", ca, crlServer.CrlUrl.ToString());

        using var httpClient = CiTestHelpers.CreateHttpClient(clientCertificate);
        using var response = await CiTestHelpers.PostCiMessageAsync(httpClient, host.BaseAddress, "8010000-IM-REVOKED", EvuRicsCode);

        Assert.False(response.IsSuccessStatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Revoked", body);
    }

    [Fact]
    public async Task Ci_ValidClientCertificateFromTrustedCaNotOnCrl_IsAccepted()
    {
        var serverCertificate = TestCertificates.CreateServerCertificate();
        var ca = CaCrlTestCertificates.CreateCa();

        await using var crlServer = await CrlHttpServer.StartAsync();
        var clientCertificate = CaCrlTestCertificates.CreateClientCertificateSignedBy(
            ca, "test-partner", out _);
        crlServer.SetCrl(CaCrlTestCertificates.BuildCrl(ca));

        await using var host = await ImApiTestHost.StartAsync(serverCertificate);
        await SeedRailwayUndertakingAsync(host);
        await SeedTwoWayOperatorAsync(host, "8010000-IM-VALID", ca, crlServer.CrlUrl.ToString());

        using var httpClient = CiTestHelpers.CreateHttpClient(clientCertificate);
        using var response = await CiTestHelpers.PostCiMessageAsync(httpClient, host.BaseAddress, "8010000-IM-VALID", EvuRicsCode);

        response.EnsureSuccessStatusCode();
        Assert.Equal("ACK", await CiTestHelpers.ExtractResponseStatusAsync(response));
    }

    private static Task SeedRailwayUndertakingAsync(ImApiTestHost host) =>
        host.RailwayUndertakings.AddAsync("Test EVU", [EvuRicsCode], "https://evu.example.test", []);

    private static async Task SeedTwoWayOperatorAsync(
        ImApiTestHost host,
        string ricsCode,
        X509Certificate2 expectedCa,
        string? clientCrlUrl)
    {
        var infrastructureOperator = await host.InfrastructureOperators.AddAsync("Two-way IM", ricsCode, "https://im.example.test");
        var bundle = await host.CertificateBundles.GetOrCreateForOperatorAsync(infrastructureOperator.Id);
        await host.CertificateBundles.SaveExpectedClientCaCertificateAsync(bundle.Id, expectedCa.Export(X509ContentType.Cert));
        await host.CertificateBundles.UpdateIdentityAsync(
            bundle.Id, expectedServerCommonName: null, expectedClientCommonName: null, clientCrlUrl, serverCrlUrl: null);
    }
}
