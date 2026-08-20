using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;

namespace TsiBroker.Core.Certificates;

// Central lookup point for a partner's (Infrastrukturbetreiber's) certificates, per BDV spec 4.3:
// the broker's own client certificate to present outbound (TICKET-3), the CA expected to have
// issued the partner's server certificate (TICKET-4), and the CA expected to have issued the
// partner's inbound client certificate (TICKET-2). Reads straight from CertificateBundleStore on
// every call (see its ResolveCertificatePath) so a certificate swap takes effect immediately,
// without a redeploy or restart.
public class PartnerCertificateProvider(CertificateBundleStore store, IOptions<CertificateBundleStoreOptions> options)
{
    public Task<PartnerCertificateBundle?> GetBundleAsync(Guid infrastructureOperatorId) =>
        store.FindByInfrastructureOperatorIdAsync(infrastructureOperatorId);

    public async Task<X509Certificate2?> GetClientCertificateAsync(Guid infrastructureOperatorId)
    {
        var bundle = await store.FindByInfrastructureOperatorIdAsync(infrastructureOperatorId);
        if (bundle?.ClientCertificateFileName is null)
        {
            return null;
        }

        var path = store.ResolveCertificatePath(bundle.ClientCertificateFileName);
        return File.Exists(path)
            ? X509CertificateLoader.LoadPkcs12FromFile(path, options.Value.PfxPassword)
            : null;
    }

    public async Task<X509Certificate2?> GetExpectedServerCaCertificateAsync(Guid infrastructureOperatorId)
    {
        var bundle = await store.FindByInfrastructureOperatorIdAsync(infrastructureOperatorId);
        return LoadPublicCertificate(bundle?.ExpectedServerCaCertificateFileName);
    }

    public async Task<X509Certificate2?> GetExpectedClientCaCertificateAsync(Guid infrastructureOperatorId)
    {
        var bundle = await store.FindByInfrastructureOperatorIdAsync(infrastructureOperatorId);
        return LoadPublicCertificate(bundle?.ExpectedClientCaCertificateFileName);
    }

    private X509Certificate2? LoadPublicCertificate(string? fileName)
    {
        if (fileName is null)
        {
            return null;
        }

        var path = store.ResolveCertificatePath(fileName);
        return File.Exists(path) ? X509CertificateLoader.LoadCertificateFromFile(path) : null;
    }
}
