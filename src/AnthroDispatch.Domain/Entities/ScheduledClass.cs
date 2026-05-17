using AnthroDispatch.Domain.Enums;
using AnthroDispatch.Domain.ValueObjects;

namespace AnthroDispatch.Domain.Entities;

public sealed class ScheduledClass
{
    public Guid Id { get; init; } = Guid.NewGuid();

    // Legacy single-group/single-instructor fields (backward compat with existing GA)
    public Guid AssignmentId { get; init; }
    public Guid GroupId { get; init; }
    public Guid InstructorId { get; init; }
    public Guid DisciplineId { get; init; }
    public Guid RoomId { get; set; }
    public TimeSlot Slot { get; set; }

    // Multi-group/multi-instructor dispatch flow additions
    public Guid? AtomicUnitId { get; init; }
    public Guid? SourceLearningAssignmentId { get; init; }
    public IReadOnlyList<Guid> GroupIds { get; init; } = [];
    public IReadOnlyList<Guid> InstructorIds { get; init; } = [];
    public LessonType LessonType { get; init; }
    public EducationForm EducationForm { get; init; }
}