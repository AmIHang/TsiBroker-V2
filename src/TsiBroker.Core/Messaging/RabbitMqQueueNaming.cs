using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TsiBroker.Core.Messaging;

public static class RabbitMqQueueNaming
{
    private static readonly Regex NonAlphanumeric = new("[^a-zA-Z0-9]+", RegexOptions.Compiled);

    // Turns a partition key (e.g. an Infrastrukturbetreiber's master-data name "ÖBB
    // Infrastruktur AG") into a stable, RabbitMQ-safe queue name
    // ("obb-infrastruktur-ag-out"). Two operators whose names slugify to the same value
    // would end up sharing a queue — acceptable for now, since Name isn't currently
    // enforced unique in InfrastructureOperatorStore.
    public static string ForPartition(string partitionKey) => $"{Slugify(partitionKey)}-out";

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
