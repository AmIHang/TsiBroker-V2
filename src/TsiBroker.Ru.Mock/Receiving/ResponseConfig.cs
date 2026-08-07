namespace TsiBroker.Ru.Mock.Receiving;

public enum MockResponseMode
{
    Ack,
    Nack,
    HttpError,

    // Canned failure modes mirroring the auth/authorization errors TsiBroker.Ru.Api returns
    // today for the (still-hypothetical) EVU->Broker direction - see TsiMessageAuthorizationService
    // - so testers can check how their broker's outbound relay handles them. Neither mode
    // actually inspects the incoming X-Api-Key/message type; they always reject, the same way
    // Nack always rejects regardless of message content.
    Unauthorized,
    Forbidden,
}

public record ResponseConfig(MockResponseMode Mode, int DelayMs)
{
    public static ResponseConfig Default { get; } = new(MockResponseMode.Ack, 0);
}

/// <summary>
/// Holds the behaviour /message should exhibit for the next incoming messages. In-memory by
/// design - it is test control state, not data, so resetting on restart is fine.
/// </summary>
public class ResponseConfigStore
{
    private volatile ResponseConfig _current = ResponseConfig.Default;

    public ResponseConfig Current
    {
        get => _current;
        set => _current = value;
    }
}
