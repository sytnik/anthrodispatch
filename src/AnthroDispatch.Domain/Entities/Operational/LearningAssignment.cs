using AnthroDispatch.Domain.Enums;

namespace AnthroDispatch.Domain.Entities.Operational;

public sealed class LearningAssignment
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public LessonType LessonType { get; init; }
    public int HoursFirstPart { get; init; }
    public int HoursSecondPart { get; init; }
    public int? PracticalHours { get; init; }
    public string? Description { get; init; }

    public Guid DepartmentId { get; init; }
    public Guid AcademicYearId { get; init; }
    public int Term { get; init; }
    public EducationForm EducationForm { get; init; }
    public EducationLanguage EducationLanguage { get; init; }
    public Guid DegreeId { get; init; }
}