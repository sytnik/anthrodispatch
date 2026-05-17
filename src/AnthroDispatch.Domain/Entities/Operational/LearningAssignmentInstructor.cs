namespace AnthroDispatch.Domain.Entities.Operational;

public sealed class LearningAssignmentInstructor
{
    public Guid LearningAssignmentId { get; init; }
    public Guid InstructorId { get; init; }
}