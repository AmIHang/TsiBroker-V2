namespace TsiBroker.Core.Certificates;

// One partner's (Infrastrukturbetreiber's) certificate configuration for the BDV mTLS exchange
// (Spec 4.3): the broker's own client certificate presented outbound to this partner, plus the
// two CAs used to validate certificates this partner presents (as a TLS server when the broker
// calls out, and as a TLS client when the partner calls in) — see PartnerCertificateProvider.
public class PartnerCertificateBundle
{
    public Guid Id { get; set; }
    public required Guid InfrastructureOperatorId { get; set; }

    // File names only (resolved against CertificateBundleStore's certificates/ subdirectory) —
    // never a full path, so moving DataDirectory (e.g. to a different mounted volume) doesn't
    // require rewriting stored metadata.

    // The broker's own client certificate (with private key, PKCS#12/.pfx) presented when the
    // broker connects out to this partner's SOAP endpoint (mTLS, TICKET-3).
    public string? ClientCertificateFileName { get; set; }

    // CA certificate (public only) that must have issued the server certificate this partner
    // presents when the broker connects out to it (TICKET-4).
    public string? ExpectedServerCaCertificateFileName { get; set; }

    // Expected Common Name / Subject Alternative Name on that server certificate — validated in
    // addition to chain trust, since a certificate issued by the right CA but for the wrong host
    // would otherwise pass (TICKET-4).
    public string? ExpectedServerCommonName { get; set; }

    // CA certificate (public only) that must have issued any client certificate this partner
    // presents when connecting in to the broker (TICKET-2).
    public string? ExpectedClientCaCertificateFileName { get; set; }

    // TICKET-1: whether this partner is provisioned for 2-way SSL (client certificate required on
    // inbound /ci calls). Uploading a client CA is what marks a partner 2-way — there's no
    // separate toggle, so this stays in sync with the bundle's actual capability automatically.
    public bool RequiresClientCertificate => ExpectedClientCaCertificateFileName is not null;

    // Expected Common Name / Subject Alternative Name on that client certificate (TICKET-2).
    public string? ExpectedClientCommonName { get; set; }

    // CRL distribution point URL for the partner's client certificate — used only as the on/off
    // switch for online revocation checking (TICKET-2, see PartnerCertificateValidator); .NET's
    // X509Chain fetches the CRL from the certificate's own embedded CDP extension, not from an
    // arbitrary URL, so this field otherwise serves as documentation of the expected endpoint.
    // Revocation checking is skipped entirely if this is not set.
    public string? ClientCrlUrl { get; set; }

    // CRL distribution point URL for the partner's server certificate (TICKET-4, spec 2.3.2 step 1
    // requires the same CA-or-CRL check outbound as inbound). Same on/off-switch semantics as
    // ClientCrlUrl: revocation checking against the server certificate is skipped if this is not
    // set.
    public string? ServerCrlUrl { get; set; }
}
