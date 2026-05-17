using AnthroDispatch.Domain.Enums;

namespace AnthroDispatch.Domain.Entities.Anthropocentric;

public sealed class HealthLimitation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid InstructorId { get; init; }
    public HealthLimitationType Type { get; init; }
    public HealthLimitationSeverity Severity { get; init; }
    public string Description { get; init; } = "";
    public bool IsHardConstraint { get; init; }
}