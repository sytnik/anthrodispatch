namespace AnthroDispatch.Domain.Entities.Operational;

public sealed class EducationalProgram
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string FullName { get; init; } = "";
    public string ShortName { get; init; } = "";
    public Guid DegreeId { get; init; }
    public Guid DepartmentId { get; init; }
    public Guid StartYearId { get; init; }
}