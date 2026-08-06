using Microsoft.Extensions.Logging;
using TsiBroker.Core.InfrastructureOperators;
using TsiBroker.Core.Messaging;

namespace TsiBroker.ApiService.InfrastructureOperators;

// Keeps each active Infrastrukturbetreiber's outbound queue consumption in sync with
// InfrastructureOperatorStore: started as soon as an operator is created/activated, stopped
// as soon as it's deactivated/deleted/renamed (renaming changes the queue name — see
// TsiMessageEndpoints.PartitionKey), and reconciled once at process startup for whatever is
// already active. The actual relay-to-IM logic doesn't exist yet (see wiki/Business-Flow.md);
// HandleMessageAsync is a logging placeholder standing in for it, the same role
// DebugMessagePublisher plays for publishing.
public class InfrastructureOperatorConsumerCoordinator(
    IMessageConsumer consumer,
    ILogger<InfrastructureOperatorConsumerCoordinator> logger)
{
    public async Task StartAllActiveAsync(IEnumerable<InfrastructureOperator> infrastructureOperators)
    {
        foreach (var infrastructureOperator in infrastructureOperators)
        {
            await StartAsync(infrastructureOperator);
        }
    }

    public Task StartAsync(InfrastructureOperator infrastructureOperator) =>
        infrastructureOperator.IsActive
            ? consumer.StartPartitionAsync(infrastructureOperator.Name, HandleMessageAsync)
            : Task.CompletedTask;

    public Task StopAsync(string infrastructureOperatorName) =>
        consumer.StopPartitionAsync(infrastructureOperatorName);

    private Task HandleMessageAsync(BrokerMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Would relay message {MessageId} from {Sender} to {Receiver} (relay-to-IM not implemented yet)",
            message.Id,
            message.Sender,
            message.Receiver);

        return Task.CompletedTask;
    }
}
