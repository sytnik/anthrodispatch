namespace AnthroDispatch.Domain.Entities.Operational;

public sealed class AcademicCalendar
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public Guid AcademicYearId { get; init; }
    public Guid DegreeId { get; init; }
}