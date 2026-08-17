using TsiBroker.Core.InfrastructureOperators;
using TsiBroker.Core.Messaging;
using TsiBroker.Core.RailwayUndertakings;

namespace TsiBroker.ApiService.RailwayUndertakings;

public enum EvuMessageAuthorizationFailureReason
{
    UnknownSender,
    NoIsbAssignment,
    MessageTypeNotAllowed,
}

public class EvuMessageAuthorizationResult
{
    private EvuMessageAuthorizationResult(EvuMessageAuthorizationFailureReason? failureReason)
    {
        FailureReason = failureReason;
    }

    public bool IsSuccess => FailureReason is null;

    public EvuMessageAuthorizationFailureReason? FailureReason { get; }

    public static EvuMessageAuthorizationResult Success() => new(null);

    public static EvuMessageAuthorizationResult Failure(EvuMessageAuthorizationFailureReason reason) => new(reason);
}

// Authorizes broker -> EVU delivery — the mirror image of TsiMessageAuthorizationService
// (TsiBroker.Ru.Api), which authorizes the opposite, EVU -> broker direction.
public class EvuMessageAuthorizationService(InfrastructureOperatorStore infrastructureOperatorStore)
{
    private const string AllowAllMessageTypes = "*";

    public async Task<EvuMessageAuthorizationResult> AuthorizeAsync(RailwayUndertaking railwayUndertaking, BrokerMessage message)
    {
        var infrastructureOperator = await infrastructureOperatorStore.FindByRicsCodeAsync(message.Sender);
        if (infrastructureOperator is null || !infrastructureOperator.IsActive)
        {
            return EvuMessageAuthorizationResult.Failure(EvuMessageAuthorizationFailureReason.UnknownSender);
        }

        var assignment = railwayUndertaking.InfrastructureOperatorAssignments.FirstOrDefault(a =>
            a.IsActive && a.InfrastructureOperatorId == infrastructureOperator.Id);
        if (assignment is null)
        {
            return EvuMessageAuthorizationResult.Failure(EvuMessageAuthorizationFailureReason.NoIsbAssignment);
        }

        var messageTypeAllowed = assignment.AllowedMessageTypesBrokerToEvu.Any(type =>
            type == AllowAllMessageTypes || string.Equals(type, message.MessageType, StringComparison.OrdinalIgnoreCase));
        if (!messageTypeAllowed)
        {
            return EvuMessageAuthorizationResult.Failure(EvuMessageAuthorizationFailureReason.MessageTypeNotAllowed);
        }

        return EvuMessageAuthorizationResult.Success();
    }
}
