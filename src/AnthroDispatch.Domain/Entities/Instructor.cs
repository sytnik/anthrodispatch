using AnthroDispatch.Domain.Enums;

namespace AnthroDispatch.Domain.Entities;

public sealed class Instructor
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = "";

    /// <summary>Legacy department name; kept for backward compatibility.</summary>
    public string Department { get; init; } = "";

    public ChronotypeCategory Chronotype { get; init; }
    public double MeqScore { get; init; }
    public int MaxClassesPerDay { get; init; }

    // Additional fields
    public Guid? DepartmentId { get; init; }
    public int Age { get; init; } = 40;
    public int MaxConsecutiveClasses { get; init; } = 3;
    public IReadOnlyList<Guid> HealthLimitationIds { get; init; } = [];
    public IReadOnlyList<Guid> InstructorConstraintIds { get; init; } = [];
}