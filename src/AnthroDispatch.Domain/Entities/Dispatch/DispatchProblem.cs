using AnthroDispatch.Domain.Entities.Anthropocentric;
using AnthroDispatch.Domain.Entities.Operational;

namespace AnthroDispatch.Domain.Entities.Dispatch;

/// <summary>
/// Central input object for the optimization core.
/// Built by DispatchInputBuilder from an AnthroDispatchDataset.
/// </summary>
public sealed class DispatchProblem
{
    public Guid Id { get; init; } = Guid.NewGuid();

    // Operational
    public IReadOnlyList<AcademicYear> AcademicYears { get; init; } = [];
    public IReadOnlyList<Degree> Degrees { get; init; } = [];
    public IReadOnlyList<Department> Departments { get; init; } = [];
    public IReadOnlyList<EducationalProgram> EducationalPrograms { get; init; } = [];
    public IReadOnlyList<CurriculumPlan> CurriculumPlans { get; init; } = [];
    public IReadOnlyList<CurriculumPlanItem> CurriculumPlanItems { get; init; } = [];
    public IReadOnlyList<CurriculumPlanItemEdge> CurriculumPlanItemEdges { get; init; } = [];

    // Core scheduling entities
    public IReadOnlyList<AcademicGroup> Groups { get; init; } = [];
    public IReadOnlyList<Instructor> Instructors { get; init; } = [];
    public IReadOnlyList<Discipline> Disciplines { get; init; } = [];
    public IReadOnlyList<Room> Rooms { get; init; } = [];

    // Learning assignments
    public IReadOnlyList<LearningAssignment> LearningAssignments { get; init; } = [];
    public IReadOnlyList<LearningAssignmentGroup> AssignmentGroups { get; init; } = [];
    public IReadOnlyList<LearningAssignmentInstructor> AssignmentInstructors { get; init; } = [];
    public IReadOnlyList<LearningAssignmentPlanItem> AssignmentPlanItems { get; init; } = [];

    // Anthropocentric
    public IReadOnlyList<HealthLimitation> HealthLimitations { get; init; } = [];
    public IReadOnlyList<InstructorConstraint> InstructorConstraints { get; init; } = [];
    public IReadOnlyList<GroupConstraint> GroupConstraints { get; init; } = [];

    // Dispatch
    public IReadOnlyList<CognitiveCompatibility> CognitiveCompatibilityMatrix { get; init; } = [];
    public IReadOnlyList<AtomicSchedulingUnit> AtomicUnits { get; init; } = [];

    public PlanningHorizon Horizon { get; init; } = PlanningHorizon.DefaultWeek();
}