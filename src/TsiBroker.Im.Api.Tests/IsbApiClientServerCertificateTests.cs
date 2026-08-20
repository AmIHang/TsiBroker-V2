using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TsiBroker.Core.Certificates;
using TsiBroker.Core.InfrastructureOperators;
using Xunit;

namespace TsiBroker.Im.Api.Tests;

// TICKET-4 coverage: outbound SOAP calls (IsbApiClient) validate the server certificate the IM
// presents back during the TLS handshake against that partner's configured CA and CRL (spec 2.3.2
// step 1 — "Kann der Key nicht gegen die CA oder CRL validiert werden, dann wird die Verbindung
// geschlossen und die Nachricht verworfen"), mirroring TICKET-2's inbound client-certificate check
// via the same PartnerCertificateValidator. Driven end-to-end through
// IsbApiClient.CheckHeartbeatAsync against a real Kestrel+TLS loopback server
// (ClientCertCapturingServer, reused here purely for the server certificate it presents) — a
// rejected handshake surfaces as CheckHeartbeatAsync returning false, the same "not reachable"
// outcome InfrastructureOperatorConsumerCoordinator already treats as a delivery failure feeding
// the existing retry/pause path, so no separate certificate-specific error handling is needed
// there.
public class IsbApiClientServerCertificateTests
{
    [Fact]
    public async Task CheckHeartbeatAsync_ServerCertificateFromUntrustedCa_IsRejected()
    {
        var expectedCa = CaCrlTestCertificates.CreateCa("Expected IM CA");
        var untrustedCa = CaCrlTestCertificates.CreateCa("Untrusted CA");
        var serverCertificate = CaCrlTestCertificates.CreateServerCertificateSignedBy(untrustedCa, "im.example.test", out _);

        await RunAsync(expectedCa, serverCrlUrl: null, serverCertificate, expectHeartbeat: false);
    }

    [Fact]
    public async Task CheckHeartbeatAsync_RevokedServerCertificate_IsRejected()
    {
        var ca = CaCrlTestCertificates.CreateCa();
        var serverCertificate = CaCrlTestCertificates.CreateServerCertificateSignedBy(ca, "im.example.test", out var serialNumber);

        await using var crlServer = await CrlHttpServer.StartAsync();
        crlServer.SetCrl(CaCrlTestCertificates.BuildCrl(ca, serialNumber));

        await RunAsync(ca, crlServer.CrlUrl.ToString(), serverCertificate, expectHeartbeat: false);
    }

    [Fact]
    public async Task CheckHeartbeatAsync_ValidServerCertificateFromTrustedCaNotOnCrl_Succeeds()
    {
        var ca = CaCrlTestCertificates.CreateCa();
        var serverCertificate = CaCrlTestCertificates.CreateServerCertificateSignedBy(ca, "im.example.test", out _);

        await using var crlServer = await CrlHttpServer.StartAsync();
        crlServer.SetCrl(CaCrlTestCertificates.BuildCrl(ca));

        await RunAsync(ca, crlServer.CrlUrl.ToString(), serverCertificate, expectHeartbeat: true);
    }

    [Fact]
    public async Task CheckHeartbeatAsync_WithNoExpectedServerCaConfigured_AcceptsAnyServerCertificate()
    {
        // Spec 2.3.2 step 1's CA/CRL check only applies once a partner has an expected server CA
        // configured — same "not every partner is provisioned yet" reasoning as the client
        // certificate side (TICKET-3). IsbApiClient.CreateHttpClientAsync skips validation entirely
        // for such a partner (not even the OS default trust-store check), rather than failing every
        // call to a partner nobody has gotten around to provisioning yet — so even a certificate
        // from a CA nobody configured, that a normal browser would reject outright, is accepted.
        var untrustedCa = CaCrlTestCertificates.CreateCa("Untrusted CA");
        var serverCertificate = CaCrlTestCertificates.CreateServerCertificateSignedBy(untrustedCa, "im.example.test", out _);

        var dataDirectory = Directory.CreateTempSubdirectory("tsibroker-isb-server-cert-tests").FullName;
        try
        {
            await using var certificateStoreApp = IsbApiClientCertificateTests.BuildCertificateStoreApp(dataDirectory);
            var store = certificateStoreApp.Services.GetRequiredService<CertificateBundleStore>();

            await using var server = await ClientCertCapturingServer.StartAsync(serverCertificate);

            var infrastructureOperator = new InfrastructureOperator
            {
                Id = Guid.NewGuid(),
                Name = "Unprovisioned IM",
                RicsCode = "8010000-IM-NOCA",
                SystemUrl = server.BaseAddress.ToString(),
            };

            var bundle = await store.GetOrCreateForOperatorAsync(infrastructureOperator.Id);
            var (clientPfxBytes, _) = IsbApiClientCertificateTests.CreateClientCertificatePfx();
            await store.SaveClientCertificateAsync(bundle.Id, clientPfxBytes);
            // Deliberately no SaveExpectedServerCaCertificateAsync call — this operator has no
            // expected server CA configured at all.

            var sut = BuildSut(certificateStoreApp);

            Assert.True(await sut.CheckHeartbeatAsync(infrastructureOperator));
        }
        finally
        {
            TryDelete(dataDirectory);
        }
    }

    private static async Task RunAsync(
        X509Certificate2 expectedCa,
        string? serverCrlUrl,
        X509Certificate2 serverCertificate,
        bool expectHeartbeat)
    {
        var dataDirectory = Directory.CreateTempSubdirectory("tsibroker-isb-server-cert-tests").FullName;
        try
        {
            await using var certificateStoreApp = IsbApiClientCertificateTests.BuildCertificateStoreApp(dataDirectory);
            var store = certificateStoreApp.Services.GetRequiredService<CertificateBundleStore>();

            await using var server = await ClientCertCapturingServer.StartAsync(serverCertificate);

            var infrastructureOperator = new InfrastructureOperator
            {
                Id = Guid.NewGuid(),
                Name = "Test IM",
                RicsCode = "8010000-IM-SERVERCERT",
                SystemUrl = server.BaseAddress.ToString(),
            };

            var bundle = await store.GetOrCreateForOperatorAsync(infrastructureOperator.Id);
            // ClientCertCapturingServer requires a client certificate at the TLS layer (see its own
            // doc comment) independent of what's under test here — without one, the handshake would
            // fail before the server certificate is ever validated, for the wrong reason.
            var (clientPfxBytes, _) = IsbApiClientCertificateTests.CreateClientCertificatePfx();
            await store.SaveClientCertificateAsync(bundle.Id, clientPfxBytes);
            await store.SaveExpectedServerCaCertificateAsync(bundle.Id, expectedCa.Export(X509ContentType.Cert));
            await store.UpdateIdentityAsync(
                bundle.Id,
                expectedServerCommonName: null,
                expectedClientCommonName: null,
                clientCrlUrl: null,
                serverCrlUrl);

            var sut = BuildSut(certificateStoreApp);

            Assert.Equal(expectHeartbeat, await sut.CheckHeartbeatAsync(infrastructureOperator));
        }
        finally
        {
            TryDelete(dataDirectory);
        }
    }

    private static IsbApiClient BuildSut(WebApplication certificateStoreApp) =>
        new(
            certificateStoreApp.Services.GetRequiredService<PartnerCertificateProvider>(),
            certificateStoreApp.Services.GetRequiredService<PartnerCertificateValidator>(),
            certificateStoreApp.Services.GetRequiredService<CrlCache>(),
            Microsoft.Extensions.Options.Options.Create(new InfrastructureOperatorDeliveryOptions()),
            certificateStoreApp.Services.GetRequiredService<ILogger<IsbApiClient>>());

    private static void TryDelete(string dataDirectory)
    {
        try
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup only.
        }
    }
}
