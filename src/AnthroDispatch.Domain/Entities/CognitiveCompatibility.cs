namespace AnthroDispatch.Domain.Entities;

public sealed class CognitiveCompatibility
{
    public Guid Id { get; init; }
    public Guid FromDisciplineId { get; init; }
    public Guid ToDisciplineId { get; init; }
    public double Score { get; init; } // [-1, 1]
}