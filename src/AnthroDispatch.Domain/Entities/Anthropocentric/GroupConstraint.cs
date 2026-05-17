using AnthroDispatch.Domain.Enums;

namespace AnthroDispatch.Domain.Entities.Anthropocentric;

public sealed class GroupConstraint
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid GroupId { get; init; }
    public ConstraintType Type { get; init; }
    public ConstraintSeverity Severity { get; init; }
    public int? Day { get; init; }
    public int? Period { get; init; }
    public string? Comment { get; init; }
}