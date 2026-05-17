namespace AnthroDispatch.Domain.Entities.Operational;

public sealed class Department
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public int Number { get; init; }
    public Guid? ParentDepartmentId { get; init; }
}