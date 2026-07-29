namespace TsiBroker.Core.RailwayUndertakings;

public class RailwayUndertaking
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required List<string> RicsCodes { get; set; }
    public required string SystemUrl { get; set; }
    public string ApiKeyEvuToBroker { get; set; } = string.Empty;
    public string ApiKeyBrokerToEvu { get; set; } = string.Empty;
    public List<IsbAssignment> InfrastructureOperatorAssignments { get; set; } = [];
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Authorizes message exchange between this EVU and one globally maintained infrastructure operator (ISB):
/// which message types the EVU may hand the broker for that ISB, and which message types the broker is
/// allowed to forward back to the EVU for that ISB.
/// </summary>
public class IsbAssignment
{
    public required Guid InfrastructureOperatorId { get; set; }
    public List<string> AllowedMessageTypesEvuToBroker { get; set; } = [];
    public List<string> AllowedMessageTypesBrokerToEvu { get; set; } = [];
    public bool IsActive { get; set; } = true;
}
