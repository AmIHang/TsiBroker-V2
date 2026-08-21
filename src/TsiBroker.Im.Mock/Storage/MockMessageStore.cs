using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace TsiBroker.Im.Mock.Storage;

/// <summary>
/// File-based log of messages the mock has seen (In/Sent). Purely a test aid - content is
/// written as plain files so it can be inspected/copied by hand. In/Sent persist across
/// restarts and are only wiped when the user hits "Clear log" (see Clear()). Out is deliberately
/// excluded from that wipe: it holds files a user dropped in that may not have been picked up yet.
/// </summary>
public class MockMessageStore
{
    private const string MetaFileSuffix = ".meta.json";

    public MockMessageStore(IHostEnvironment env, IOptions<MockMessageStoreOptions> options)
    {
        var dataDirectory = string.IsNullOrWhiteSpace(options.Value.DataDirectory)
            ? Path.Combine(env.ContentRootPath, "App_Data")
            : options.Value.DataDirectory;

        InDirectory = Path.Combine(dataDirectory, "In");
        OutDirectory = Path.Combine(dataDirectory, "Out");
        SentDirectory = Path.Combine(dataDirectory, "Sent");
    }

    public string InDirectory { get; }
    public string OutDirectory { get; }
    public string SentDirectory { get; }

    /// <summary>
    /// Deletes everything under App_Data except Out/ (see class remarks for why), then recreates
    /// In/Sent so a subsequent List() or file-system browse sees empty directories rather than
    /// missing ones. Only called from the "Clear log" endpoint - never on startup.
    /// </summary>
    public void Clear()
    {
        DeleteDirectory(InDirectory);
        DeleteDirectory(SentDirectory);
        Directory.CreateDirectory(InDirectory);
        Directory.CreateDirectory(SentDirectory);
    }

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    public async Task SaveReceivedAsync(
        string content,
        string? messageIdentifier,
        string result,
        CancellationToken cancellationToken = default)
    {
        await SaveAsync(InDirectory, content, messageIdentifier, result, responseContent: null, cancellationToken);
    }

    // responseContent is the broker's raw response to this sent message (e.g. the ACK/NACK
    // envelope) - captured here since CiClient only returns it to the caller otherwise, and the
    // UI's send dialog closes right after sending rather than displaying it inline.
    public async Task SaveSentAsync(
        string content,
        string? messageIdentifier,
        string result,
        string? responseContent,
        CancellationToken cancellationToken = default)
    {
        await SaveAsync(SentDirectory, content, messageIdentifier, result, responseContent, cancellationToken);
    }

    public IReadOnlyList<MockMessageEntry> List()
    {
        var entries = new List<MockMessageEntry>();
        entries.AddRange(ReadDirectory(InDirectory, MockMessageDirection.Received));
        entries.AddRange(ReadDirectory(SentDirectory, MockMessageDirection.Sent));
        return entries.OrderByDescending(e => e.Timestamp).ToList();
    }

    private static async Task SaveAsync(
        string directory,
        string content,
        string? messageIdentifier,
        string result,
        string? responseContent,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        var fileName = BuildFileName(messageIdentifier);
        await File.WriteAllTextAsync(Path.Combine(directory, fileName), content, cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(directory, fileName + MetaFileSuffix),
            JsonSerializer.Serialize(new MessageMeta(messageIdentifier, result, responseContent)),
            cancellationToken);
    }

    private static IEnumerable<MockMessageEntry> ReadDirectory(string directory, MockMessageDirection direction)
    {
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var filePath in Directory.EnumerateFiles(directory, "*.xml"))
        {
            var meta = ReadMeta(filePath + MetaFileSuffix);
            yield return new MockMessageEntry(
                FileName: Path.GetFileName(filePath),
                Direction: direction,
                Timestamp: File.GetLastWriteTimeUtc(filePath),
                MessageIdentifier: meta?.MessageIdentifier,
                Result: meta?.Result,
                Content: File.ReadAllText(filePath),
                ResponseContent: meta?.ResponseContent);
        }
    }

    private static MessageMeta? ReadMeta(string metaPath)
    {
        if (!File.Exists(metaPath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<MessageMeta>(File.ReadAllText(metaPath));
    }

    private static string BuildFileName(string? messageIdentifier)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd'T'HHmmssfff");
        var safeIdentifier = Sanitize(messageIdentifier);
        return safeIdentifier.Length > 0
            ? $"{timestamp}_{safeIdentifier}.xml"
            : $"{timestamp}.xml";
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = value.Where(c => !invalidChars.Contains(c)).Take(60).ToArray();
        return new string(chars);
    }

    private record MessageMeta(string? MessageIdentifier, string Result, string? ResponseContent = null);
}
