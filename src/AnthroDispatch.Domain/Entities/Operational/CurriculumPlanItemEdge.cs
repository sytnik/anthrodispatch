namespace AnthroDispatch.Domain.Entities.Operational;

public sealed class CurriculumPlanItemEdge
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ParentPlanItemId { get; init; }
    public Guid ChildPlanItemId { get; init; }
}