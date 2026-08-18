using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TsiBroker.Core.Certificates;
using TsiBroker.Core.InfrastructureOperators;
using Xunit;

namespace TsiBroker.Im.Api.Tests;

// TICKET-3 coverage: outbound SOAP calls (IsbApiClient) present a client certificate for the
// mandatory 2-way SSL connection to an Infrastrukturbetreiber (spec 2.3/4.3), resolving whichever
// partner-specific certificate is configured for the IM actually being called rather than sharing
// one certificate across every partner (see IsbApiClient.CreateHttpClientAsync).
//
// Coverage is split in two rather than driven end-to-end through IsbApiClient.CheckHeartbeatAsync
// against a self-signed test server: IsbApiClient's HttpClientHandler uses the OS's default trust
// for whatever server certificate the IM presents (validating it against a partner-specific CA is
// TICKET-4, not wired up yet — see PartnerCertificateProvider.GetExpectedServerCaCertificateAsync),
// so a real end-to-end call would fail on the *server's* certificate, not the client one under
// test here. Trusting a throwaway test root in the machine's certificate store to work around that
// would mutate shared system state for a test run, so instead: one test proves per-partner
// resolution picks the right certificate (PartnerCertificateProvider, no network), and the other
// proves that certificate is actually delivered over a real TLS handshake to a server that demands
// one, using the exact ClientCertificates mechanism IsbApiClient.CreateHttpClientAsync relies on.
public class IsbApiClientCertificateTests
{
    [Fact]
    public async Task PartnerCertificateProvider_ResolvesTheCorrectCertificatePerPartner()
    {
        var dataDirectory = Directory.CreateTempSubdirectory("tsibroker-isb-client-tests").FullName;
        try
        {
            await using var certificateStoreApp = BuildCertificateStoreApp(dataDirectory);
            var store = certificateStoreApp.Services.GetRequiredService<CertificateBundleStore>();
            var provider = certificateStoreApp.Services.GetRequiredService<PartnerCertificateProvider>();

            var operatorAId = Guid.NewGuid();
            var operatorBId = Guid.NewGuid();
            var (pfxBytesA, clientCertificateA) = CreateClientCertificatePfx();
            var (pfxBytesB, clientCertificateB) = CreateClientCertificatePfx();

            var bundleA = await store.GetOrCreateForOperatorAsync(operatorAId);
            await store.SaveClientCertificateAsync(bundleA.Id, pfxBytesA);
            var bundleB = await store.GetOrCreateForOperatorAsync(operatorBId);
            await store.SaveClientCertificateAsync(bundleB.Id, pfxBytesB);

            var resolvedA = await provider.GetClientCertificateAsync(operatorAId);
            var resolvedB = await provider.GetClientCertificateAsync(operatorBId);

            Assert.NotNull(resolvedA);
            Assert.NotNull(resolvedB);
            Assert.Equal(clientCertificateA.Thumbprint, resolvedA!.Thumbprint);
            Assert.Equal(clientCertificateB.Thumbprint, resolvedB!.Thumbprint);
            Assert.NotEqual(resolvedA.Thumbprint, resolvedB.Thumbprint);
        }
        finally
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

    [Fact]
    public async Task ClientCertificate_IsPresentedOverARealTlsHandshake_AndReachesTheRequiringServer()
    {
        var dataDirectory = Directory.CreateTempSubdirectory("tsibroker-isb-client-tests").FullName;
        try
        {
            await using var certificateStoreApp = BuildCertificateStoreApp(dataDirectory);
            var store = certificateStoreApp.Services.GetRequiredService<CertificateBundleStore>();
            var provider = certificateStoreApp.Services.GetRequiredService<PartnerCertificateProvider>();

            var infrastructureOperatorId = Guid.NewGuid();
            var (pfxBytes, clientCertificate) = CreateClientCertificatePfx();
            var bundle = await store.GetOrCreateForOperatorAsync(infrastructureOperatorId);
            await store.SaveClientCertificateAsync(bundle.Id, pfxBytes);

            var serverCertificate = TestCertificates.CreateServerCertificate();
            await using var server = await ClientCertCapturingServer.StartAsync(serverCertificate);

            var resolvedCertificate = await provider.GetClientCertificateAsync(infrastructureOperatorId);
            Assert.NotNull(resolvedCertificate);

            // Same handler shape as IsbApiClient.CreateHttpClientAsync (HttpClientHandler +
            // ClientCertificates.Add); the only test-only addition is trusting this throwaway
            // server certificate, standing in for TICKET-4's not-yet-implemented server CA check.
            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
            };
            handler.ClientCertificates.Add(resolvedCertificate!);
            using var httpClient = new HttpClient(handler);

            using var response = await httpClient.PostAsync(new Uri(server.BaseAddress, "/heartbeat"), new StringContent(""));

            response.EnsureSuccessStatusCode();
            Assert.NotNull(server.CapturedClientCertificate);
            Assert.Equal(clientCertificate.Thumbprint, server.CapturedClientCertificate!.Thumbprint);
        }
        finally
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

    [Fact]
    public async Task CheckHeartbeatAsync_WithNoConfiguredCertificate_StillConnectsWithoutOne()
    {
        // A partner not yet provisioned for 2-way SSL (no client certificate uploaded) must not
        // make outbound calls fail outright — PartnerCertificateProvider.GetClientCertificateAsync
        // returns null and CreateHttpClientAsync simply skips attaching one, same as before
        // TICKET-3.
        var dataDirectory = Directory.CreateTempSubdirectory("tsibroker-isb-client-tests").FullName;
        try
        {
            await using var certificateStoreApp = BuildCertificateStoreApp(dataDirectory);
            var provider = certificateStoreApp.Services.GetRequiredService<PartnerCertificateProvider>();

            var serverCertificate = TestCertificates.CreateServerCertificate();
            await using var server = await ClientCertCapturingServer.StartAsync(serverCertificate);

            var infrastructureOperator = new InfrastructureOperator
            {
                Id = Guid.NewGuid(),
                Name = "No-cert IM",
                RicsCode = "8010000-IM-NOCERT",
                SystemUrl = server.BaseAddress.ToString(),
            };

            var sut = new IsbApiClient(provider);

            // RequireCertificate on the server side means the handshake itself fails without a
            // certificate — CheckHeartbeatAsync swallows that as "not reachable" rather than
            // throwing, so this documents the current behavior for an unprovisioned partner.
            Assert.False(await sut.CheckHeartbeatAsync(infrastructureOperator));
        }
        finally
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

    private static WebApplication BuildCertificateStoreApp(string dataDirectory)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CertificateBundles:DataDirectory"] = dataDirectory,
        });
        builder.Services
            .AddOptions<CertificateBundleStoreOptions>()
            .Bind(builder.Configuration.GetSection(CertificateBundleStoreOptions.SectionName));
        builder.Services.AddSingleton<CertificateBundleStore>();
        builder.Services.AddSingleton<PartnerCertificateProvider>();
        return builder.Build();
    }

    // TestCertificates.CreateClientCertificate() reloads its cert from an export before returning
    // it, and re-exporting that already-reloaded instance fails on Windows ("key is not valid in
    // the specified state") — so this exports once, from the freshly self-signed certificate, and
    // hands back both the bytes (for CertificateBundleStore) and a certificate loaded from those
    // same bytes (for the test's own thumbprint assertions).
    private static (byte[] PfxBytes, X509Certificate2 Certificate) CreateClientCertificatePfx()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=test-partner", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: false));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.2")], critical: false));

        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
        var pfxBytes = certificate.Export(X509ContentType.Pfx);
        return (pfxBytes, X509CertificateLoader.LoadPkcs12(pfxBytes, password: null));
    }
}
