using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace TsiBroker.Core.Certificates;

public enum CertificateValidationFailureReason
{
    NotYetValid,
    Expired,
    NoExpectedCaCertificateConfigured,
    CommonNameMismatch,
    ChainNotTrusted,
    Revoked,
}

public record CertificateValidationResult(bool IsValid, CertificateValidationFailureReason? FailureReason = null)
{
    public static readonly CertificateValidationResult Success = new(true);
    public static CertificateValidationResult Failure(CertificateValidationFailureReason reason) => new(false, reason);
}

// Validates a certificate a partner presents against BDV spec 4.3's checks: expiry, chain trust to
// the partner's own CA (not the machine trust store — each partner's CA is configured per
// PartnerCertificateBundle, since these are partner-issued certificates a public root store
// wouldn't recognize), CN/SAN identity, and CRL revocation status. Shared between both directions:
// the client certificate a partner presents inbound (TICKET-2) and the server certificate a
// partner presents when the broker connects out to it (TICKET-4, spec 2.3.2 step 1 requires the
// same "CA or CRL" check outbound) — only which bundle fields (CA/CRL/CN) apply differs.
public class PartnerCertificateValidator(CrlCache crlCache, ILogger<PartnerCertificateValidator> logger)
{
    // TICKET-4: the server certificate the partner's own system presents when the broker connects
    // out to it. Takes an already-fetched CRL rather than a URL: the caller
    // (HttpClientHandler.ServerCertificateCustomValidationCallback in IsbApiClient) is a
    // synchronous TLS-handshake hook, so the async CRL fetch has to happen before the handshake
    // starts, not inside this call. Pass null when bundle.ServerCrlUrl isn't set, to skip the
    // revocation check the same way ValidateClientCertificateAsync does.
    public CertificateValidationResult ValidateServerCertificate(
        PartnerCertificateBundle? bundle,
        X509Certificate2 serverCertificate,
        X509Certificate2? expectedCaCertificate,
        CachedCrl? crl) =>
        ValidateChainIdentityAndRevocation(serverCertificate, expectedCaCertificate, bundle?.ExpectedServerCommonName, crl);

    // TICKET-2: the client certificate a partner presents when connecting in to the broker.
    public async Task<CertificateValidationResult> ValidateClientCertificateAsync(
        PartnerCertificateBundle bundle,
        X509Certificate2 clientCertificate,
        X509Certificate2? expectedCaCertificate)
    {
        var crl = string.IsNullOrWhiteSpace(bundle.ClientCrlUrl) ? null : await crlCache.GetAsync(bundle.ClientCrlUrl);
        return ValidateChainIdentityAndRevocation(clientCertificate, expectedCaCertificate, bundle.ExpectedClientCommonName, crl);
    }

    // Expiry, chain trust to the configured CA, CN/SAN identity, and — if a CRL was fetched —
    // revocation. The shared core both ValidateServerCertificate and ValidateClientCertificateAsync
    // delegate to, so the CA/CRL check only needs to be gotten right once for both directions.
    private CertificateValidationResult ValidateChainIdentityAndRevocation(
        X509Certificate2 certificate,
        X509Certificate2? expectedCaCertificate,
        string? expectedCommonName,
        CachedCrl? crl)
    {
        var chainResult = ValidateChainAndIdentity(certificate, expectedCaCertificate, expectedCommonName);
        if (!chainResult.IsValid || crl is null)
        {
            return chainResult;
        }

        var serialNumberHex = Convert.ToHexString(certificate.SerialNumberBytes.Span);
        return crl.RevokedSerialNumbersHex.Contains(serialNumberHex)
            ? CertificateValidationResult.Failure(CertificateValidationFailureReason.Revoked)
            : CertificateValidationResult.Success;
    }

    // Expiry, chain trust to the configured CA, and CN/SAN identity — everything except revocation,
    // which ValidateChainIdentityAndRevocation layers on top once a CRL (if any) has been fetched.
    private CertificateValidationResult ValidateChainAndIdentity(
        X509Certificate2 certificate,
        X509Certificate2? expectedCaCertificate,
        string? expectedCommonName)
    {
        // NotBefore/NotAfter are expressed in local time (X509Certificate2 convention) — compare
        // against DateTime.Now, not UTC.
        var now = DateTime.Now;
        if (now < certificate.NotBefore)
        {
            return CertificateValidationResult.Failure(CertificateValidationFailureReason.NotYetValid);
        }

        if (now > certificate.NotAfter)
        {
            return CertificateValidationResult.Failure(CertificateValidationFailureReason.Expired);
        }

        if (expectedCaCertificate is null)
        {
            return CertificateValidationResult.Failure(CertificateValidationFailureReason.NoExpectedCaCertificateConfigured);
        }

        if (!string.IsNullOrWhiteSpace(expectedCommonName) && !MatchesExpectedIdentity(certificate, expectedCommonName))
        {
            return CertificateValidationResult.Failure(CertificateValidationFailureReason.CommonNameMismatch);
        }

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(expectedCaCertificate);
        // Revocation is handled separately via CrlCache (an explicit, cached fetch of
        // PartnerCertificateBundle.ClientCrlUrl) rather than here: X509Chain's own online mode
        // fetches from the certificate's embedded CDP extension on every single call with no
        // application-level cache, which is both a fresh network round trip per request and not
        // actually pointed at the partner-configured URL.
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

        if (!chain.Build(certificate))
        {
            logger.LogWarning(
                "Certificate chain validation failed for {Subject}: {Statuses}",
                certificate.Subject,
                string.Join(", ", chain.ChainStatus.Select(s => s.StatusInformation.Trim())));
            return CertificateValidationResult.Failure(CertificateValidationFailureReason.ChainNotTrusted);
        }

        return CertificateValidationResult.Success;
    }

    // Matches against the Subject Alternative Name (DNS entries) first, since that's what modern
    // TLS validation prefers, and falls back to the legacy Subject CN only if no SAN is present at
    // all — some partner CAs still only set a CN.
    private static bool MatchesExpectedIdentity(X509Certificate2 certificate, string expectedIdentity)
    {
        var sanExtension = certificate.Extensions["2.5.29.17"];
        if (sanExtension is not null)
        {
            var san = new X509SubjectAlternativeNameExtension(sanExtension.RawData, sanExtension.Critical);
            var dnsNames = san.EnumerateDnsNames().ToList();
            if (dnsNames.Count > 0)
            {
                return dnsNames.Any(name => string.Equals(name, expectedIdentity, StringComparison.OrdinalIgnoreCase));
            }
        }

        var commonName = certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
        return string.Equals(commonName, expectedIdentity, StringComparison.OrdinalIgnoreCase);
    }
}
