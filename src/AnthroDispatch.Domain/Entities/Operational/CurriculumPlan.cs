using AnthroDispatch.Domain.Enums;

namespace AnthroDispatch.Domain.Entities.Operational;

public sealed class CurriculumPlan
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public Guid EducationalProgramId { get; init; }
    public Guid StartYearId { get; init; }
    public Guid CalendarId { get; init; }
    public EducationForm EducationForm { get; init; }
    public EducationLanguage EducationLanguage { get; init; }
    public bool ReadyForScheduling { get; init; }
    public bool IsLocked { get; init; }
}