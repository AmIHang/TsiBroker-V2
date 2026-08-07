using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace TsiBroker.Ru.Mock.Storage;

/// <summary>
/// File-based log of messages the mock has seen (In/Sent). Purely a test aid - content is
/// written as plain files so it can be inspected/copied by hand, and In/Sent are wiped on every
/// startup since this history never needs to survive a restart. Out is left alone: it holds
/// files a user dropped in that may not have been picked up yet.
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

    public void ResetOnStartup()
    {
        ResetDirectory(InDirectory);
        Directory.CreateDirectory(OutDirectory);
        ResetDirectory(SentDirectory);
    }

    public async Task SaveReceivedAsync(
        string content,
        string? messageIdentifier,
        string result,
        CancellationToken cancellationToken = default)
    {
        await SaveAsync(InDirectory, content, messageIdentifier, result, cancellationToken);
    }

    public async Task SaveSentAsync(
        string content,
        string? messageIdentifier,
        string result,
        CancellationToken cancellationToken = default)
    {
        await SaveAsync(SentDirectory, content, messageIdentifier, result, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        var fileName = BuildFileName(messageIdentifier);
        await File.WriteAllTextAsync(Path.Combine(directory, fileName), content, cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(directory, fileName + MetaFileSuffix),
            JsonSerializer.Serialize(new MessageMeta(messageIdentifier, result)),
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
                Content: File.ReadAllText(filePath));
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

    private static void ResetDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }

        Directory.CreateDirectory(directory);
    }

    private record MessageMeta(string? MessageIdentifier, string Result);
}
