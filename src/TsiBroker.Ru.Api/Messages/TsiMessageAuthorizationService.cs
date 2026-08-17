using TsiBroker.Core.InfrastructureOperators;
using TsiBroker.Core.Messaging;
using TsiBroker.Core.RailwayUndertakings;

namespace TsiBroker.Ru.Api.Messages;

public enum TsiMessageAuthorizationFailureReason
{
    InvalidApiKey,
    SenderMismatch,
    UnknownRecipient,
    NoIsbAssignment,
    MessageTypeNotAllowed,
}

public class TsiMessageAuthorizationResult
{
    private TsiMessageAuthorizationResult(
        RailwayUndertaking? railwayUndertaking,
        InfrastructureOperator? infrastructureOperator,
        TsiMessageAuthorizationFailureReason? failureReason)
    {
        RailwayUndertaking = railwayUndertaking;
        InfrastructureOperator = infrastructureOperator;
        FailureReason = failureReason;
    }

    public bool IsSuccess => FailureReason is null;

    public RailwayUndertaking? RailwayUndertaking { get; }

    // The recipient ISB, resolved via the message's Recipient RICS code — determines the
    // outbound queue the message is placed on (see TsiMessageEndpoints.PartitionKey).
    public InfrastructureOperator? InfrastructureOperator { get; }

    public TsiMessageAuthorizationFailureReason? FailureReason { get; }

    public static TsiMessageAuthorizationResult Success(RailwayUndertaking railwayUndertaking, InfrastructureOperator infrastructureOperator) =>
        new(railwayUndertaking, infrastructureOperator, null);

    public static TsiMessageAuthorizationResult Failure(TsiMessageAuthorizationFailureReason reason) =>
        new(null, null, reason);
}

public class TsiMessageAuthorizationService(
    RailwayUndertakingStore railwayUndertakingStore,
    InfrastructureOperatorStore infrastructureOperatorStore)
{
    private const string AllowAllMessageTypes = "*";

    public async Task<TsiMessageAuthorizationResult> AuthorizeAsync(string apiKey, IncomingTsiMessage message)
    {
        var railwayUndertaking = await railwayUndertakingStore.FindByApiKeyEvuToBrokerAsync(apiKey);
        if (railwayUndertaking is null || !railwayUndertaking.IsActive)
        {
            return TsiMessageAuthorizationResult.Failure(TsiMessageAuthorizationFailureReason.InvalidApiKey);
        }

        var senderMatches = railwayUndertaking.RicsCodes.Any(code =>
            string.Equals(code, message.Sender, StringComparison.OrdinalIgnoreCase));
        if (!senderMatches)
        {
            return TsiMessageAuthorizationResult.Failure(TsiMessageAuthorizationFailureReason.SenderMismatch);
        }

        var infrastructureOperator = await infrastructureOperatorStore.FindByRicsCodeAsync(message.Recipient);
        if (infrastructureOperator is null || !infrastructureOperator.IsActive)
        {
            return TsiMessageAuthorizationResult.Failure(TsiMessageAuthorizationFailureReason.UnknownRecipient);
        }

        var assignment = railwayUndertaking.InfrastructureOperatorAssignments.FirstOrDefault(a =>
            a.IsActive && a.InfrastructureOperatorId == infrastructureOperator.Id);
        if (assignment is null)
        {
            return TsiMessageAuthorizationResult.Failure(TsiMessageAuthorizationFailureReason.NoIsbAssignment);
        }

        var messageTypeAllowed = assignment.AllowedMessageTypesEvuToBroker.Any(type =>
            type == AllowAllMessageTypes || string.Equals(type, message.MessageType, StringComparison.OrdinalIgnoreCase));
        if (!messageTypeAllowed)
        {
            return TsiMessageAuthorizationResult.Failure(TsiMessageAuthorizationFailureReason.MessageTypeNotAllowed);
        }

        return TsiMessageAuthorizationResult.Success(railwayUndertaking, infrastructureOperator);
    }
}
