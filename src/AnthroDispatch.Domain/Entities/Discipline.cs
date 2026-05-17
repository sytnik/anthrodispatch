using AnthroDispatch.Domain.Enums;

namespace AnthroDispatch.Domain.Entities;

public sealed class Discipline
{
    public Guid Id { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public CognitiveProcessType ProcessType { get; init; }
    public CognitiveLoadLevel LoadLevel { get; init; }
    public DisciplineDomain Domain { get; init; }
}