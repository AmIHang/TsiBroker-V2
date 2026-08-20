using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace TsiBroker.Im.Api.Tests;

// TICKET-1 coverage: /ci rejects a partner provisioned for 2-way SSL (PartnerCertificateBundle
// with ExpectedClientCaCertificateFileName set) when it presents no client certificate, and leaves
// 1-way partners (today's only real-world case) unaffected. The "presented and trusted" acceptance
// path also exercises TICKET-2's CA check (there's no way to separate presence from trust once
// PartnerCertificateValidator is wired in) — CaCrlValidationTests covers the CA/CN/CRL failure
// cases in depth.
public class ClientCertificateEnforcementTests
{
    private const string EvuRicsCode = "8010000-EVU";

    [Fact]
    public async Task Ci_TwoWayPartner_WithoutClientCertificate_IsRejected()
    {
        var serverCertificate = TestCertificates.CreateServerCertificate();
        var expectedCa = CaCrlTestCertificates.CreateCa();
        await using var host = await ImApiTestHost.StartAsync(serverCertificate);
        await SeedRailwayUndertakingAsync(host);
        await SeedTwoWayInfrastructureOperatorAsync(host, "8010000-IM-2WAY-A", expectedCa);

        using var httpClient = CiTestHelpers.CreateHttpClient(clientCertificate: null);
        using var response = await CiTestHelpers.PostCiMessageAsync(httpClient, host.BaseAddress, "8010000-IM-2WAY-A", EvuRicsCode);

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
        var expectedCa = CaCrlTestCertificates.CreateCa();
        var clientCertificate = CaCrlTestCertificates.CreateClientCertificateSignedBy(
            expectedCa, "test-partner", out _);
        await using var host = await ImApiTestHost.StartAsync(serverCertificate);
        await SeedRailwayUndertakingAsync(host);
        await SeedTwoWayInfrastructureOperatorAsync(host, "8010000-IM-2WAY-B", expectedCa);

        using var httpClient = CiTestHelpers.CreateHttpClient(clientCertificate);
        using var response = await CiTestHelpers.PostCiMessageAsync(httpClient, host.BaseAddress, "8010000-IM-2WAY-B", EvuRicsCode);

        response.EnsureSuccessStatusCode();
        Assert.Equal("ACK", await CiTestHelpers.ExtractResponseStatusAsync(response));
    }

    [Fact]
    public async Task Ci_OneWayPartner_WithoutClientCertificate_IsAccepted()
    {
        var serverCertificate = TestCertificates.CreateServerCertificate();
        await using var host = await ImApiTestHost.StartAsync(serverCertificate);
        await SeedRailwayUndertakingAsync(host);
        await host.InfrastructureOperators.AddAsync("One-way IM", "8010000-IM-1WAY", "https://im.example.test");

        using var httpClient = CiTestHelpers.CreateHttpClient(clientCertificate: null);
        using var response = await CiTestHelpers.PostCiMessageAsync(httpClient, host.BaseAddress, "8010000-IM-1WAY", EvuRicsCode);

        response.EnsureSuccessStatusCode();
        Assert.Equal("ACK", await CiTestHelpers.ExtractResponseStatusAsync(response));
    }

    private static Task SeedRailwayUndertakingAsync(ImApiTestHost host) =>
        host.RailwayUndertakings.AddAsync("Test EVU", [EvuRicsCode], "https://evu.example.test", []);

    private static async Task SeedTwoWayInfrastructureOperatorAsync(ImApiTestHost host, string ricsCode, X509Certificate2 expectedCa)
    {
        var infrastructureOperator = await host.InfrastructureOperators.AddAsync("Two-way IM", ricsCode, "https://im.example.test");
        var bundle = await host.CertificateBundles.GetOrCreateForOperatorAsync(infrastructureOperator.Id);
        await host.CertificateBundles.SaveExpectedClientCaCertificateAsync(bundle.Id, expectedCa.Export(X509ContentType.Cert));
    }
}
