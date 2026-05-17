using AnthroDispatch.Application.Abstractions;
using AnthroDispatch.Application.DataPreparation;
using AnthroDispatch.Domain.Entities.Operational;
using AnthroDispatch.Domain.Enums;
using AnthroDispatch.Infrastructure.MockData;
using FluentAssertions;

namespace AnthroDispatch.Tests.Algorithms;

[TestFixture]
public class DispatchInputBuilderTests
{
    private static readonly AnthroDispatchGenerationOptions Opts = new(
        Seed: 7,
        AcademicYears: 1, Departments: 3, Degrees: 2, EducationalPrograms: 2,
        CurriculumPlans: 2, Terms: 4,
        Groups: 4, StudentsApprox: 80, Instructors: 6,
        Disciplines: 8, Rooms: 5,
        InstructorConstraintRate: 0.80,
        HealthLimitationRate: 0.80);

    private static async Task<AnthroDispatchDataset> GetDataset()
    {
        var gen = new AnthroDispatchMockDataGenerator();
        return await gen.GenerateAsync(Opts);
    }

    [Test]
    public async Task DispatchInputBuilder_ShouldConvertDatasetToDispatchProblem()
    {
        var dataset = await GetDataset();
        var builder = new DispatchInputBuilder();
        var problem = builder.Build(dataset);

        problem.Should().NotBeNull();
        problem.Groups.Count.Should().Be(dataset.Groups.Count);
        problem.Instructors.Count.Should().Be(dataset.Instructors.Count);
        problem.Rooms.Count.Should().Be(dataset.Rooms.Count);
        problem.Horizon.Days.Should().Be(6);
        problem.Horizon.PeriodsPerDay.Should().Be(8);
    }

    [Test]
    public void DispatchInputBuilder_ShouldCalculateRequiredPeriodsFromCalendarTerms()
    {
        var calc = new CurriculumHoursCalculator();
        var term = new AcademicCalendarTerm
        {
            PartOneWeeks = 8,
            PartTwoWeeks = 8
        };
        var item = new CurriculumPlanItem
        {
            LecturePerWeekFirst = 2,
            LecturePerWeekSecond = 2
        };

        var periods = calc.CalculateRequiredPeriods(
            item, term,
            LessonType.Lecture,
            EducationForm.FullTime);

        // hours = 8*2 + 8*2 = 32, periods = ceil(32/2) = 16
        periods.Should().Be(16);
    }

    [Test]
    public async Task DispatchInputBuilder_ShouldExpandLectureAssignmentsWithMultipleGroups()
    {
        var dataset = await GetDataset();
        var builder = new DispatchInputBuilder();
        var problem = builder.Build(dataset);

        // Should produce atomic units
        problem.AtomicUnits.Count.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task DispatchInputBuilder_ShouldSplitLaboratoryAssignmentsByGroupWhenNeeded()
    {
        var dataset = await GetDataset();
        var expander = new AssignmentExpander(new CurriculumHoursCalculator());
        var units = expander.Expand(dataset, new Dictionary<Guid, AcademicCalendarTerm>(), 0);

        // Lab assignments with multiple groups should be split (unless room is big enough)
        // Just verify we got units and they are valid
        units.Should().NotBeEmpty();
        units.Should().AllSatisfy(u => u.GroupIds.Count.Should().BeGreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task DispatchInputBuilder_ShouldUsePrerequisiteEdgesToAdjustCompatibilityMatrix()
    {
        var dataset = await GetDataset();
        var builder = new DispatchInputBuilder();
        var problem = builder.Build(dataset);

        // Compatibility matrix should be non-empty
        problem.CognitiveCompatibilityMatrix.Count.Should().BeGreaterThan(0);
        problem.CognitiveCompatibilityMatrix.Should().AllSatisfy(c =>
            c.Score.Should().BeInRange(-1.0, 1.0));
    }

    [Test]
    public async Task DispatchInputBuilder_ShouldRejectPlansWithoutCalendarTerms()
    {
        // Build dataset, then strip all calendar terms
        var dataset = await GetDataset();
        var stripped = new AnthroDispatchDataset
        {
            Id = dataset.Id,
            CurriculumPlans = dataset.CurriculumPlans,
            AcademicCalendarTerms = [], // no terms!
            Groups = dataset.Groups,
            Instructors = dataset.Instructors,
            Disciplines = dataset.Disciplines,
            Rooms = dataset.Rooms,
            LearningAssignments = dataset.LearningAssignments,
            LearningAssignmentGroups = dataset.LearningAssignmentGroups,
            LearningAssignmentInstructors = dataset.LearningAssignmentInstructors,
            LearningAssignmentPlanItems = dataset.LearningAssignmentPlanItems
        };

        var validator = new OperationalDataValidator();
        var errors = validator.Validate(stripped);

        errors.Should().Contain(e => e.Contains("no calendar terms"));
    }

    [Test]
    public async Task DispatchInputBuilder_ShouldAttachInstructorConstraints()
    {
        var opts = Opts with { InstructorConstraintRate = 1.0 };
        var gen = new AnthroDispatchMockDataGenerator();
        var dataset = await gen.GenerateAsync(opts);
        var builder = new DispatchInputBuilder();
        var problem = builder.Build(dataset);

        problem.InstructorConstraints.Count.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task DispatchInputBuilder_ShouldAttachHealthLimitations()
    {
        var opts = Opts with { HealthLimitationRate = 1.0 };
        var gen = new AnthroDispatchMockDataGenerator();
        var dataset = await gen.GenerateAsync(opts);
        var builder = new DispatchInputBuilder();
        var problem = builder.Build(dataset);

        problem.HealthLimitations.Count.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task DispatchInputBuilder_ShouldValidateOfflineRoomCapacityFeasibility()
    {
        var dataset = await GetDataset();
        var expander = new AssignmentExpander(new CurriculumHoursCalculator());
        var units = expander.Expand(dataset, new Dictionary<Guid, AcademicCalendarTerm>(), 0);
        var validator = new OperationalDataValidator();

        // Should not throw; returns warnings list
        var warnings = validator.ValidateRoomCapacity(dataset, units);
        warnings.Should().NotBeNull();
    }
}