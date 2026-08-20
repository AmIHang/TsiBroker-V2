using System.Collections.Concurrent;
using System.Formats.Asn1;
using Microsoft.Extensions.Logging;

namespace TsiBroker.Core.Certificates;

public sealed record CachedCrl(IReadOnlySet<string> RevokedSerialNumbersHex, DateTimeOffset ExpiresAt);

// Explicit CRL fetch + cache (spec 4.3 / TICKET-2 item 5): X509Chain's own RevocationMode.Online
// fetches per chain-build call with no application-visible cache, so PartnerCertificateValidator
// no longer uses it at all — every client-certificate validation would otherwise mean a fresh CRL
// download from the partner's server. Registered as a singleton (see Program.cs) so the cache
// persists across requests; a single long-lived HttpClient is deliberate too, since this only ever
// talks to a small, fixed set of partner CRL endpoints, not the general "IHttpClientFactory to
// avoid DNS staleness" scenario.
public class CrlCache(HttpClient httpClient, ILogger<CrlCache> logger)
{
    // Falls back to this when a fetched CRL has no (or an already-past) nextUpdate — keeps a
    // misconfigured or stale-looking CRL from being refetched on literally every request while
    // still re-checking often enough to notice a newly revoked certificate.
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<string, CachedCrl> _cache = new();

    public async Task<CachedCrl> GetAsync(string crlUrl)
    {
        if (_cache.TryGetValue(crlUrl, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return cached;
        }

        var crlBytes = await httpClient.GetByteArrayAsync(crlUrl);
        var (revokedSerialNumbersHex, nextUpdate) = CrlParser.Parse(crlBytes);
        var expiresAt = nextUpdate is { } next && next > DateTimeOffset.UtcNow ? next : DateTimeOffset.UtcNow + DefaultTtl;

        var entry = new CachedCrl(revokedSerialNumbersHex, expiresAt);
        _cache[crlUrl] = entry;
        logger.LogInformation(
            "Fetched CRL from {CrlUrl}: {RevokedCount} revoked entries, cached until {ExpiresAt}",
            crlUrl,
            revokedSerialNumbersHex.Count,
            expiresAt);
        return entry;
    }
}

// Hand-rolled parser for the subset of RFC 5280 §5.1's CertificateList structure this cache needs
// (revoked serial numbers and nextUpdate) — the BCL has CertificateRevocationListBuilder to build
// a CRL but no public API to parse one back.
internal static class CrlParser
{
    public static (HashSet<string> RevokedSerialNumbersHex, DateTimeOffset? NextUpdate) Parse(byte[] crlBytes)
    {
        var reader = new AsnReader(crlBytes, AsnEncodingRules.DER);
        var certificateList = reader.ReadSequence();
        var tbsCertList = certificateList.ReadSequence();

        if (tbsCertList.PeekTag() == Asn1Tag.Integer)
        {
            tbsCertList.ReadInteger(); // version, unused
        }

        tbsCertList.ReadSequence(); // signature AlgorithmIdentifier, unused
        tbsCertList.ReadSequence(); // issuer Name, unused

        ReadTime(tbsCertList); // thisUpdate, unused

        DateTimeOffset? nextUpdate = null;
        if (tbsCertList.HasData && IsTimeTag(tbsCertList.PeekTag()))
        {
            nextUpdate = ReadTime(tbsCertList);
        }

        var revokedSerialNumbersHex = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (tbsCertList.HasData && tbsCertList.PeekTag() == Asn1Tag.Sequence)
        {
            var revokedCertificates = tbsCertList.ReadSequence();
            while (revokedCertificates.HasData)
            {
                var entry = revokedCertificates.ReadSequence();
                var serialNumber = entry.ReadIntegerBytes();
                revokedSerialNumbersHex.Add(Convert.ToHexString(serialNumber.Span));
                // revocationDate + optional crlEntryExtensions follow; not needed, entry discarded.
            }
        }

        // crlExtensions [0] EXPLICIT, if present, is not needed either.
        return (revokedSerialNumbersHex, nextUpdate);
    }

    private static bool IsTimeTag(Asn1Tag tag) =>
        tag.TagClass == TagClass.Universal && tag.TagValue is 23 or 24; // UTCTime, GeneralizedTime

    private static DateTimeOffset ReadTime(AsnReader reader) =>
        reader.PeekTag().TagValue == 23 ? reader.ReadUtcTime() : reader.ReadGeneralizedTime();
}
