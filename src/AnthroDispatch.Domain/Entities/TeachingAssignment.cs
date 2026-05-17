using AnthroDispatch.Domain.Enums;

namespace AnthroDispatch.Domain.Entities;

public sealed class TeachingAssignment
{
    public Guid Id { get; init; }
    public Guid GroupId { get; init; }
    public Guid InstructorId { get; init; }
    public Guid DisciplineId { get; init; }
    public ClassType ClassType { get; init; }
    public int RequiredPeriods { get; init; }
}