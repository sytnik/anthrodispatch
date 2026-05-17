using AnthroDispatch.Domain.Enums;

namespace AnthroDispatch.Domain.Entities.Dispatch;

/// <summary>
/// Atomic scheduling unit — the minimal unit passed to the optimization core.
/// One learning assignment may expand into multiple atomic units (e.g., laboratory splits by group).
/// </summary>
public sealed class AtomicSchedulingUnit
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid SourceLearningAssignmentId { get; init; }
    public Guid DisciplineId { get; init; }

    public IReadOnlyList<Guid> GroupIds { get; init; } = [];
    public IReadOnlyList<Guid> InstructorIds { get; init; } = [];

    public LessonType LessonType { get; init; }
    public RoomType RequiredRoomType { get; init; }
    public EducationForm EducationForm { get; init; }
    public EducationLanguage EducationLanguage { get; init; }

    public int RequiredPeriods { get; init; }
    public int Term { get; init; }

    public bool IsOnline =>
        EducationForm == EducationForm.Distance ||
        RequiredRoomType == RoomType.Online;
}