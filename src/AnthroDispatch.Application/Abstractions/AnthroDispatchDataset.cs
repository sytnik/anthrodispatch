using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.Entities.Anthropocentric;
using AnthroDispatch.Domain.Entities.Operational;

namespace AnthroDispatch.Application.Abstractions;

public sealed class AnthroDispatchDataset
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public List<AcademicYear> AcademicYears { get; init; } = [];
    public List<Degree> Degrees { get; init; } = [];
    public List<Department> Departments { get; init; } = [];
    public List<EducationalProgram> EducationalPrograms { get; init; } = [];
    public List<AcademicCalendar> AcademicCalendars { get; init; } = [];
    public List<AcademicCalendarTerm> AcademicCalendarTerms { get; init; } = [];
    public List<CurriculumPlan> CurriculumPlans { get; init; } = [];
    public List<CurriculumPlanItem> CurriculumPlanItems { get; init; } = [];
    public List<CurriculumPlanItemEdge> CurriculumPlanItemEdges { get; init; } = [];

    public List<AcademicGroup> Groups { get; init; } = [];
    public List<Instructor> Instructors { get; init; } = [];
    public List<Discipline> Disciplines { get; init; } = [];
    public List<Room> Rooms { get; init; } = [];

    public List<LearningAssignment> LearningAssignments { get; init; } = [];
    public List<LearningAssignmentGroup> LearningAssignmentGroups { get; init; } = [];
    public List<LearningAssignmentInstructor> LearningAssignmentInstructors { get; init; } = [];
    public List<LearningAssignmentPlanItem> LearningAssignmentPlanItems { get; init; } = [];

    public List<HealthLimitation> HealthLimitations { get; init; } = [];
    public List<InstructorConstraint> InstructorConstraints { get; init; } = [];
    public List<GroupConstraint> GroupConstraints { get; init; } = [];

    public List<CognitiveCompatibility> CognitiveCompatibilities { get; init; } = [];
}