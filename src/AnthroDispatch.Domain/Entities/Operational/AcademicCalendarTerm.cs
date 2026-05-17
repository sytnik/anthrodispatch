namespace AnthroDispatch.Domain.Entities.Operational;

public sealed class AcademicCalendarTerm
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CalendarId { get; init; }
    public int Term { get; init; }
    public int PartOneWeeks { get; init; }
    public int PartTwoWeeks { get; init; }
    public Guid TermOccurrenceYearId { get; init; }
}