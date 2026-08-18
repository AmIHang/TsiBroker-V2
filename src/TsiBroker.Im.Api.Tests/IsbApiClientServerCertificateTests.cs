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

    // "No expected server CA configured" (a partner not yet provisioned, per
    // IsbApiClient.CreateHttpClientAsync) isn't covered here as a network test: skipping our custom
    // ServerCertificateCustomValidationCallback just leaves HttpClientHandler's own default OS
    // trust store validation in place, so proving the skip took effect — rather than merely
    // observing a self-signed test certificate getting rejected either way, by us or by the OS —
    // would need trusting a throwaway CA in the machine's certificate store, which this test suite
    // deliberately avoids (see IsbApiClientCertificateTests' own class comment for the same
    // reasoning).

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
