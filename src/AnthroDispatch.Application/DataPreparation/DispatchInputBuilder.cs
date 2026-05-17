using AnthroDispatch.Application.Abstractions;
using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.Entities.Dispatch;
using AnthroDispatch.Domain.Entities.Operational;

namespace AnthroDispatch.Application.DataPreparation;

/// <summary>
/// Converts an AnthroDispatchDataset into a DispatchProblem.
/// </summary>
public sealed class DispatchInputBuilder : IDispatchInputBuilder
{
    private readonly ICurriculumHoursCalculator _calculator;
    private readonly AssignmentExpander _expander;
    private readonly OperationalDataValidator _validator;

    public DispatchInputBuilder(
        ICurriculumHoursCalculator? calculator = null,
        AssignmentExpander? expander = null,
        OperationalDataValidator? validator = null)
    {
        _calculator = calculator ?? new CurriculumHoursCalculator();
        _expander = expander ?? new AssignmentExpander(_calculator);
        _validator = validator ?? new OperationalDataValidator();
    }

    public DispatchProblem Build(AnthroDispatchDataset dataset, DispatchBuildOptions? options = null)
    {
        options ??= new DispatchBuildOptions(); // todo

        // Step 1: validate
        var errors = _validator.Validate(dataset);
        // We log but do not throw — prototype tolerates partial data

        // Step 2: build term lookup (calendarId, termNumber) → term
        var termLookup = dataset.AcademicCalendarTerms
            .GroupBy(t => (t.CalendarId, t.Term))
            .ToDictionary(g => g.Key, g => g.First());

        // Step 3: expand assignments into atomic scheduling units
        var units = _expander.Expand(dataset, new Dictionary<Guid, AcademicCalendarTerm>(), 0);

        // Step 4: validate room capacity
        _validator.ValidateRoomCapacity(dataset, units);

        // Step 5: build cognitive compatibility matrix (use dataset's matrix + prerequisite edge boost)
        var compatMatrix = EnrichCompatibilityMatrix(dataset);

        return new DispatchProblem
        {
            AcademicYears = dataset.AcademicYears,
            Degrees = dataset.Degrees,
            Departments = dataset.Departments,
            EducationalPrograms = dataset.EducationalPrograms,
            CurriculumPlans = dataset.CurriculumPlans,
            CurriculumPlanItems = dataset.CurriculumPlanItems,
            CurriculumPlanItemEdges = dataset.CurriculumPlanItemEdges,
            Groups = dataset.Groups,
            Instructors = dataset.Instructors,
            Disciplines = dataset.Disciplines,
            Rooms = dataset.Rooms,
            LearningAssignments = dataset.LearningAssignments,
            AssignmentGroups = dataset.LearningAssignmentGroups,
            AssignmentInstructors = dataset.LearningAssignmentInstructors,
            AssignmentPlanItems = dataset.LearningAssignmentPlanItems,
            HealthLimitations = dataset.HealthLimitations,
            InstructorConstraints = dataset.InstructorConstraints,
            GroupConstraints = dataset.GroupConstraints,
            CognitiveCompatibilityMatrix = compatMatrix,
            AtomicUnits = units,
            Horizon = PlanningHorizon.DefaultWeek()
        };
    }

    /// <summary>
    /// Apply prerequisite-edge influence: items linked by prerequisite edge get a small positive
    /// boost to their cognitive compatibility score (adjacent terms often have complementary content).
    /// </summary>
    private static List<CognitiveCompatibility> EnrichCompatibilityMatrix(AnthroDispatchDataset dataset)
    {
        var matrix = dataset.CognitiveCompatibilities.ToDictionary(
            c => (c.FromDisciplineId, c.ToDisciplineId),
            c => c.Score);

        // Build discipline lookup per plan item
        var planItemDiscipline = dataset.CurriculumPlanItems
            .ToDictionary(p => p.Id, p => p.DisciplineId);

        foreach (var edge in dataset.CurriculumPlanItemEdges)
        {
            if (!planItemDiscipline.TryGetValue(edge.ParentPlanItemId, out var dParent)) continue;
            if (!planItemDiscipline.TryGetValue(edge.ChildPlanItemId, out var dChild)) continue;
            if (dParent == dChild) continue;

            const double boost = 0.10;
            var key = (dParent, dChild);
            matrix[key] = Math.Clamp(matrix.GetValueOrDefault(key, 0.0) + boost, -1.0, 1.0);
        }

        return matrix.Select(kv => new CognitiveCompatibility
        {
            Id = Guid.NewGuid(),
            FromDisciplineId = kv.Key.Item1,
            ToDisciplineId = kv.Key.Item2,
            Score = kv.Value
        }).ToList();
    }
}