using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;
using TsiBroker.Core.Certificates;
using TsiBroker.Core.InfrastructureOperators;

namespace TsiBroker.ApiService.Certificates;

// Daily sweep over every configured certificate (the broker's own client certificate plus each
// partner's expected server/client CA) that logs a warning once a certificate is within
// CertificateBundleStoreOptions.ExpiryWarningThresholdDays of expiring, and an error once it's
// already expired — so rotation (spec 4.3) happens ahead of an outage instead of being discovered
// as one. Runs once immediately on startup rather than waiting out the first interval.
public class CertificateExpiryMonitor(
    CertificateBundleStore store,
    InfrastructureOperatorStore infrastructureOperatorStore,
    IOptions<CertificateBundleStoreOptions> options,
    ILogger<CertificateExpiryMonitor> logger) : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await SweepAsync(stoppingToken);

            try
            {
                await Task.Delay(SweepInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Shutting down.
            }
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        var bundles = await store.GetAllAsync();
        var threshold = TimeSpan.FromDays(options.Value.ExpiryWarningThresholdDays);

        foreach (var bundle in bundles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var infrastructureOperator = await infrastructureOperatorStore.FindByIdAsync(bundle.InfrastructureOperatorId);
            var operatorName = infrastructureOperator?.Name ?? bundle.InfrastructureOperatorId.ToString();

            CheckFile(operatorName, "client certificate", bundle.ClientCertificateFileName, threshold, isPkcs12: true);
            CheckFile(operatorName, "expected server CA", bundle.ExpectedServerCaCertificateFileName, threshold, isPkcs12: false);
            CheckFile(operatorName, "expected client CA", bundle.ExpectedClientCaCertificateFileName, threshold, isPkcs12: false);
        }
    }

    // isPkcs12 picks the loader by the file's actual role/format (client cert = PFX, CA certs =
    // plain), not by whether a PFX password happens to be configured — a password-less PFX (a
    // legitimate, common case) would otherwise be mis-loaded as a plain certificate and fail.
    private void CheckFile(string operatorName, string role, string? fileName, TimeSpan threshold, bool isPkcs12)
    {
        if (fileName is null)
        {
            return;
        }

        var path = store.ResolveCertificatePath(fileName);
        if (!File.Exists(path))
        {
            logger.LogWarning("Configured {Role} for {OperatorName} is missing on disk: {Path}", role, operatorName, path);
            return;
        }

        X509Certificate2 certificate;
        try
        {
            certificate = isPkcs12
                ? X509CertificateLoader.LoadPkcs12FromFile(path, options.Value.PfxPassword)
                : X509CertificateLoader.LoadCertificateFromFile(path);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not load {Role} for {OperatorName} from {Path}", role, operatorName, path);
            return;
        }

        using (certificate)
        {
            // NotAfter is expressed in local time (X509Certificate2 convention).
            var remaining = certificate.NotAfter - DateTime.Now;
            if (remaining < TimeSpan.Zero)
            {
                logger.LogError("{Role} for {OperatorName} expired on {NotAfter:u}", role, operatorName, certificate.NotAfter);
            }
            else if (remaining < threshold)
            {
                logger.LogWarning(
                    "{Role} for {OperatorName} expires on {NotAfter:u} (in {DaysRemaining} days) — rotate soon",
                    role, operatorName, certificate.NotAfter, (int)remaining.TotalDays);
            }
        }
    }
}
