namespace TsiBroker.Ru.Mock.Storage;

public enum MockMessageDirection
{
    // Broker -> Mock (the mock played the EVU receiving a message).
    Received,

    // Mock -> Broker (the mock played the EVU sending a message).
    Sent,
}

public record MockMessageEntry(
    string FileName,
    MockMessageDirection Direction,
    DateTimeOffset Timestamp,
    string? MessageIdentifier,
    string? Result,
    string Content,
    string? ResponseContent);
