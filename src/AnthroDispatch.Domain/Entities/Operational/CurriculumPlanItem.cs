namespace AnthroDispatch.Domain.Entities.Operational;

public sealed class CurriculumPlanItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CurriculumPlanId { get; init; }
    public Guid DisciplineId { get; init; }
    public Guid DepartmentReaderId { get; init; }
    public int Term { get; init; }
    public decimal Credits { get; init; }
    public string? ComponentCode { get; init; }
    public bool IsMandatoryLocked { get; init; }

    public bool HasExam { get; init; }
    public bool HasTest { get; init; }
    public bool HasGradedTest { get; init; }
    public bool HasCourseWork { get; init; }
    public bool HasCourseProject { get; init; }

    public int LecturePerWeekFirst { get; init; }
    public int LecturePerWeekSecond { get; init; }
    public int LabWorkPerWeekFirst { get; init; }
    public int LabWorkPerWeekSecond { get; init; }
    public int PracticalWorkPerWeekFirst { get; init; }
    public int PracticalWorkPerWeekSecond { get; init; }

    public int LabWorkDistanceSessionHours { get; init; }
    public int PracticalWorkDistanceSessionHours { get; init; }

    public int CreditHours => (int)(Credits * 30);
}