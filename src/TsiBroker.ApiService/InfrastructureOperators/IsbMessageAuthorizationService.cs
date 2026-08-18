using TsiBroker.Core.InfrastructureOperators;
using TsiBroker.Core.Messaging;
using TsiBroker.Core.RailwayUndertakings;

namespace TsiBroker.ApiService.InfrastructureOperators;

public enum IsbMessageAuthorizationFailureReason
{
    UnknownSender,
    NoIsbAssignment,
    MessageTypeNotAllowed,
}

public class IsbMessageAuthorizationResult
{
    private IsbMessageAuthorizationResult(IsbMessageAuthorizationFailureReason? failureReason)
    {
        FailureReason = failureReason;
    }

    public bool IsSuccess => FailureReason is null;

    public IsbMessageAuthorizationFailureReason? FailureReason { get; }

    public static IsbMessageAuthorizationResult Success() => new(null);

    public static IsbMessageAuthorizationResult Failure(IsbMessageAuthorizationFailureReason reason) => new(reason);
}

// Authorizes broker -> IM delivery — the mirror image of EvuMessageAuthorizationService
// (broker -> EVU) and TsiMessageAuthorizationService (EVU -> broker, TsiBroker.Ru.Api). Rechecked
// here at delivery time — rather than only trusting the EVU -> broker submission-time check in
// TsiMessageAuthorizationService — so an assignment edited while the message was queued still
// takes effect before final delivery.
public class IsbMessageAuthorizationService(RailwayUndertakingStore railwayUndertakingStore)
{
    private const string AllowAllMessageTypes = "*";

    public async Task<IsbMessageAuthorizationResult> AuthorizeAsync(InfrastructureOperator infrastructureOperator, BrokerMessage message)
    {
        var railwayUndertaking = await railwayUndertakingStore.FindByRicsCodeAsync(message.Sender);
        if (railwayUndertaking is null || !railwayUndertaking.IsActive)
        {
            return IsbMessageAuthorizationResult.Failure(IsbMessageAuthorizationFailureReason.UnknownSender);
        }

        var assignment = railwayUndertaking.InfrastructureOperatorAssignments.FirstOrDefault(a =>
            a.IsActive && a.InfrastructureOperatorId == infrastructureOperator.Id);
        if (assignment is null)
        {
            return IsbMessageAuthorizationResult.Failure(IsbMessageAuthorizationFailureReason.NoIsbAssignment);
        }

        var messageTypeAllowed = assignment.AllowedMessageTypesEvuToBroker.Any(type =>
            type == AllowAllMessageTypes || string.Equals(type, message.MessageType, StringComparison.OrdinalIgnoreCase));
        if (!messageTypeAllowed)
        {
            return IsbMessageAuthorizationResult.Failure(IsbMessageAuthorizationFailureReason.MessageTypeNotAllowed);
        }

        return IsbMessageAuthorizationResult.Success();
    }
}
