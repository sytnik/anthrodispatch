namespace AnthroDispatch.Domain.Entities;

public sealed class DatasetRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public int Seed { get; init; }

    // Core counts (original)
    public int Groups { get; init; }
    public int Instructors { get; init; }
    public int Disciplines { get; init; }
    public int Rooms { get; init; }
    public int Assignments { get; init; }

    // Extended counts
    public int CurriculumPlans { get; init; }
    public int CurriculumPlanItems { get; init; }
    public int CalendarTerms { get; init; }
    public int LearningAssignments { get; init; }
    public int InstructorConstraints { get; init; }
    public int HealthLimitations { get; init; }

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}