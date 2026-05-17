namespace AnthroDispatch.Domain.Entities.Operational;

public sealed class AcademicYear
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public int StartYear { get; init; }
    public string Name { get; init; } = ""; // e.g., "2025/2026"
}