namespace TsiBroker.Im.Mock.Receiving;

public enum MockResponseMode
{
    Ack,
    Nack,
    HttpError,
}

public record ResponseConfig(MockResponseMode Mode, int DelayMs)
{
    public static ResponseConfig Default { get; } = new(MockResponseMode.Ack, 0);
}

/// <summary>
/// Holds the behaviour /ci should exhibit for the next incoming messages. In-memory by design -
/// it is test control state, not data, so resetting on restart is fine.
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
