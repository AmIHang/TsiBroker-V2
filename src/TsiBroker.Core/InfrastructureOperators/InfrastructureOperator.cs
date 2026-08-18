namespace TsiBroker.Core.InfrastructureOperators;

public class InfrastructureOperator
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string RicsCode { get; set; }
    public required string SystemUrl { get; set; }
    public bool IsActive { get; set; } = true;

    // Whether outbound (broker -> IM) queue processing is currently paused for this operator,
    // because its system was found unreachable (see InfrastructureOperatorReachabilityMonitor).
    // Independent of IsActive, which governs whether the operator can be addressed at all.
    public bool IsQueuePaused { get; set; }
    public string? PauseReason { get; set; }
    public DateTimeOffset? PausedAtUtc { get; set; }

    // Index into InfrastructureOperatorReachabilityMonitor's backoff schedule — persisted so a
    // process restart resumes the schedule instead of silently restarting it from the shortest
    // interval.
    public int PauseBackoffStep { get; set; }
}
