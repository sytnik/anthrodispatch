using AnthroDispatch.Domain.Enums;

namespace AnthroDispatch.Domain.Entities.Anthropocentric;

public sealed class InstructorConstraint
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid InstructorId { get; init; }
    public ConstraintType Type { get; init; }
    public ConstraintSeverity Severity { get; init; }
    public int? Day { get; init; }
    public int? Period { get; init; }
    public Guid? RoomId { get; init; }
    public string? BuildingCode { get; init; }
    public string? Comment { get; init; }
}