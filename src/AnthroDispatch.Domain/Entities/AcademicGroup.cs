using AnthroDispatch.Domain.Enums;

namespace AnthroDispatch.Domain.Entities;

public sealed class AcademicGroup
{
    public Guid Id { get; init; }
    public string Code { get; init; } = "";

    /// <summary>Legacy name; kept for backward compatibility.</summary>
    public string ProgramName { get; init; } = "";

    public int StudentCount { get; init; }
    public ChronotypeCategory Chronotype { get; init; }
    public double MeanMeqScore { get; init; }

    // Additional fields
    public Guid? EducationalProgramId { get; init; }
    public Guid? CurriculumPlanId { get; init; }
    public double AverageAge { get; init; } = 20.0;
    public double AgeStdDev { get; init; } = 1.5;
    public IReadOnlyList<Guid> HealthLimitationIds { get; init; } = [];
    public IReadOnlyList<Guid> GroupConstraintIds { get; init; } = [];
}