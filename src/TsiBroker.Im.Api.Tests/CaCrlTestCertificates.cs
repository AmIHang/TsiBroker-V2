using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace TsiBroker.Im.Api.Tests;

// TICKET-2 coverage needs a real CA hierarchy (unlike TestCertificates' bare self-signed leaves,
// used only for TICKET-1's presence-only check) so PartnerCertificateValidator's X509Chain-based
// trust check has something real to build against.
internal static class CaCrlTestCertificates
{
    public static X509Certificate2 CreateCa(string commonName = "Test Partner CA")
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest($"CN={commonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(certificateAuthority: true, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, critical: true));
        // Without a Subject Key Identifier, CertificateRevocationListBuilder falls back to an
        // issuer-name+serial-number Authority Key Identifier on the CRL — Windows' chain engine
        // then fails to associate that CRL with this CA when checking a leaf's revocation status
        // (silently treating the certificate as if it had no usable CRL at all, rather than
        // failing the check), so real CAs always carry one and test CAs need to as well.
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));

        var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(30));
        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), password: null);
    }

    // issuerWithPrivateKey must carry its private key (as returned by CreateCa) since it's used to
    // sign the leaf.
    public static X509Certificate2 CreateClientCertificateSignedBy(
        X509Certificate2 issuerWithPrivateKey,
        string commonName,
        out byte[] serialNumber)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest($"CN={commonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: false));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.2")], critical: false));
        // Ties this leaf to the issuing CA's Subject Key Identifier — see the comment on
        // CreateCa's own SKI for why chain validation needs this.
        request.CertificateExtensions.Add(
            X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuerWithPrivateKey, includeKeyIdentifier: true, includeIssuerAndSerial: false));

        using var publicCertificate = request.Create(
            issuerWithPrivateKey,
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(1),
            RandomNumberGenerator.GetBytes(8));
        var withPrivateKey = publicCertificate.CopyWithPrivateKey(rsa);
        var certificate = X509CertificateLoader.LoadPkcs12(withPrivateKey.Export(X509ContentType.Pfx), password: null);

        // CertificateRequest.Create DER-normalizes the serial number it's given (prepending a
        // 0x00 byte when the high bit of the first byte is set, so it can't be misread as a
        // negative INTEGER) — but CertificateRevocationListBuilder.AddEntry doesn't apply that
        // same normalization to whatever bytes it's handed. Returning the certificate's own
        // already-normalized SerialNumberBytes (rather than the pre-normalization random bytes)
        // keeps the two callers of this value in sync; passing the raw bytes to both intermittently
        // built a CRL entry that didn't actually match the certificate it was meant to revoke.
        serialNumber = certificate.SerialNumberBytes.ToArray();
        return certificate;
    }

    // Builds a DER-encoded CRL, optionally listing the given serial numbers as revoked, signed by
    // the given CA. issuerWithPrivateKey must carry its private key.
    public static byte[] BuildCrl(X509Certificate2 issuerWithPrivateKey, params byte[][] revokedSerialNumbers)
    {
        var builder = new CertificateRevocationListBuilder();
        foreach (var serialNumber in revokedSerialNumbers)
        {
            builder.AddEntry(serialNumber, DateTimeOffset.UtcNow.AddMinutes(-1));
        }

        return builder.Build(
            issuerWithPrivateKey,
            crlNumber: 1,
            nextUpdate: DateTimeOffset.UtcNow.AddDays(7),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
    }
}
