namespace AnthroDispatch.Domain.Entities.Operational;

public sealed class LearningAssignmentPlanItem
{
    public Guid LearningAssignmentId { get; init; }
    public Guid CurriculumPlanItemId { get; init; }
    public Guid DisciplineId { get; init; }
}