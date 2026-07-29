namespace TsiBroker.Core.InfrastructureOperators;

public class InfrastructureOperator
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string RicsCode { get; set; }
    public required string SystemUrl { get; set; }
    public bool IsActive { get; set; } = true;
}
