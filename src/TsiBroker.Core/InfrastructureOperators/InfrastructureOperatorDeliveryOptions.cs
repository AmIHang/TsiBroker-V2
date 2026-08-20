namespace TsiBroker.Core.InfrastructureOperators;

// Governs broker -> IM SOAP delivery (spec 2.3.2) — how long IsbApiClient waits for an ACK/NACK
// per attempt, and how InfrastructureOperatorConsumerCoordinator resends a message that keeps
// timing out.
public class InfrastructureOperatorDeliveryOptions
{
    public const string SectionName = "InfrastructureOperatorDelivery";

    // Spec 2.3.2 step 2c: "Der BDV-Client empfängt innerhalb von 5 Sekunden keine Response."
    public TimeSpan DeliveryTimeout { get; set; } = TimeSpan.FromSeconds(5);

    // How long a message keeps being resent after a delivery timeout (or a transport-level
    // failure) before InfrastructureOperatorConsumerCoordinator gives up and dead-letters it —
    // spec 2.3.2 step 2c: "erneut gesendet, bis das maximale Alter der Nachricht erreicht ist".
    // The spec doesn't name a value; TICKET-6 flagged this as needing a business decision, so
    // this default is a placeholder pending sign-off, not a spec-derived number.
    public TimeSpan MaxMessageAge { get; set; } = TimeSpan.FromHours(24);

    // Delay between resend attempts once a delivery has timed out or failed transport-wise. Kept
    // well above DeliveryTimeout so a struggling partner isn't hit again the instant one attempt
    // times out.
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMinutes(1);
}
