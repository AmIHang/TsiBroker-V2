using TsiBroker.Im.Mock.Storage;

namespace TsiBroker.Im.Mock.Sending;

/// <summary>
/// Polls Out/ for dropped XML files and forwards each one to the configured broker /ci endpoint,
/// archiving the result to Sent/. Polling instead of FileSystemWatcher to avoid picking up a file
/// while it's still being written and to keep behaviour predictable across platforms.
/// </summary>
public class OutboxWatcher(
    MockMessageStore store,
    CiClient client,
    ILogger<OutboxWatcher> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MinimumFileAge = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessOutboxAsync(stoppingToken);

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Shutting down.
            }
        }
    }

    private async Task ProcessOutboxAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(store.OutDirectory))
        {
            return;
        }

        foreach (var filePath in Directory.EnumerateFiles(store.OutDirectory, "*.xml"))
        {
            if (DateTime.UtcNow - File.GetLastWriteTimeUtc(filePath) < MinimumFileAge)
            {
                continue;
            }

            try
            {
                var payload = await File.ReadAllTextAsync(filePath, cancellationToken);
                var messageIdentifier = Path.GetFileNameWithoutExtension(filePath);

                var result = await client.SendAsync(
                    payload,
                    messageIdentifier: messageIdentifier,
                    cancellationToken: cancellationToken);
                await store.SaveSentAsync(payload, messageIdentifier, result.Status, cancellationToken);

                File.Delete(filePath);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send dropped file {FilePath}", filePath);
            }
        }
    }
}
