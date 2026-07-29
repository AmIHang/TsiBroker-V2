namespace TsiBroker.Core.RailwayUndertakings;

public class RailwayUndertaking
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required List<string> RicsCodes { get; set; }
    public required string SystemUrl { get; set; }
    public string ApiKeyEvuToBroker { get; set; } = string.Empty;
    public string ApiKeyBrokerToEvu { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
