using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TsiBroker.Core.Messaging;

public static class RabbitMqQueueNaming
{
    private static readonly Regex NonAlphanumeric = new("[^a-zA-Z0-9]+", RegexOptions.Compiled);

    // Turns a partition key (e.g. an RU's master-data name "ÖBB Rail Cargo Austria") into
    // a stable, RabbitMQ-safe queue name ("obb-rail-cargo-austria-in"). Two RUs whose names
    // slugify to the same value would end up sharing a queue — acceptable for now, since RU
    // names aren't currently enforced unique in RailwayUndertakingStore.
    public static string ForPartition(string partitionKey) => $"{Slugify(partitionKey)}-in";

    private static string Slugify(string value)
    {
        var normalized = value.Replace("ß", "ss").Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        var slug = NonAlphanumeric
            .Replace(builder.ToString(), "-")
            .Trim('-')
            .ToLowerInvariant();

        return string.IsNullOrEmpty(slug) ? "unknown" : slug;
    }
}
