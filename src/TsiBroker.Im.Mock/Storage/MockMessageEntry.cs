namespace TsiBroker.Im.Mock.Storage;

public enum MockMessageDirection
{
    // Broker -> Mock (the mock played the ISB receiving a message).
    Received,

    // Mock -> Broker (the mock played the ISB sending a message).
    Sent,
}

public record MockMessageEntry(
    string FileName,
    MockMessageDirection Direction,
    DateTimeOffset Timestamp,
    string? MessageIdentifier,
    string? Result,
    string Content);
