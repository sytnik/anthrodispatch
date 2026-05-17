using AnthroDispatch.Application.Abstractions;
using AnthroDispatch.Infrastructure.MockData;
using FluentAssertions;

namespace AnthroDispatch.Tests.Algorithms;

[TestFixture]
public class AnthroDispatchGeneratorTests
{
    private static readonly AnthroDispatchGenerationOptions SmallOptions = new(
        Seed: 42,
        AcademicYears: 2, Departments: 4, Degrees: 2, EducationalPrograms: 3,
        CurriculumPlans: 3, Terms: 4,
        Groups: 6, StudentsApprox: 120, Instructors: 10,
        Disciplines: 12, Rooms: 8,
        InstructorConstraintRate: 0.50,
        HealthLimitationRate: 0.30);

    [Test]
    public async Task AnthroDispatchGenerator_ShouldCreateCurriculumPlans()
    {
        var gen = new AnthroDispatchMockDataGenerator();
        var dataset = await gen.GenerateAsync(SmallOptions);

        dataset.CurriculumPlans.Count.Should().Be(SmallOptions.CurriculumPlans);
        dataset.CurriculumPlans.Should().AllSatisfy(p => p.Id.Should().NotBeEmpty());
        dataset.CurriculumPlans.Should().AllSatisfy(p => p.ReadyForScheduling.Should().BeTrue());
    }

    [Test]
    public async Task AnthroDispatchGenerator_ShouldCreateCalendarTerms()
    {
        var gen = new AnthroDispatchMockDataGenerator();
        var dataset = await gen.GenerateAsync(SmallOptions);

        dataset.AcademicCalendarTerms.Count.Should().BeGreaterThan(0);
        dataset.AcademicCalendarTerms.Should().AllSatisfy(t =>
        {
            t.PartOneWeeks.Should().BeGreaterThan(0);
            t.PartTwoWeeks.Should().BeGreaterThan(0);
        });
    }

    [Test]
    public async Task AnthroDispatchGenerator_ShouldCreatePlanItemsForEachPlan()
    {
        var gen = new AnthroDispatchMockDataGenerator();
        var dataset = await gen.GenerateAsync(SmallOptions);

        dataset.CurriculumPlanItems.Count.Should().BeGreaterThan(0);
        foreach (var plan in dataset.CurriculumPlans)
        {
            var items = dataset.CurriculumPlanItems.Where(i => i.CurriculumPlanId == plan.Id).ToList();
            items.Count.Should().BeGreaterThan(0, $"Plan {plan.Name} should have plan items");
        }
    }

    [Test]
    public async Task AnthroDispatchGenerator_ShouldCreateLearningAssignmentsFromPlanItems()
    {
        var gen = new AnthroDispatchMockDataGenerator();
        var dataset = await gen.GenerateAsync(SmallOptions);

        dataset.LearningAssignments.Count.Should().BeGreaterThan(0);
        // Each learning assignment must have at least one group link
        foreach (var la in dataset.LearningAssignments)
        {
            var groupLinks = dataset.LearningAssignmentGroups
                .Where(x => x.LearningAssignmentId == la.Id).ToList();
            groupLinks.Count.Should().BeGreaterThanOrEqualTo(1,
                $"LearningAssignment {la.Id} should have group links");
        }
    }

    [Test]
    public async Task AnthroDispatchGenerator_ShouldCreatePrerequisiteEdgesFromEarlierToLaterTerms()
    {
        var gen = new AnthroDispatchMockDataGenerator();
        var dataset = await gen.GenerateAsync(SmallOptions);

        var planItemDict = dataset.CurriculumPlanItems.ToDictionary(i => i.Id);
        foreach (var edge in dataset.CurriculumPlanItemEdges)
        {
            if (planItemDict.TryGetValue(edge.ParentPlanItemId, out var parent) &&
                planItemDict.TryGetValue(edge.ChildPlanItemId, out var child))
            {
                parent.Term.Should().BeLessThanOrEqualTo(child.Term,
                    "Prerequisite edges should go from earlier to later terms");
            }
        }
    }

    [Test]
    public async Task AnthroDispatchGenerator_ShouldCreateInstructorConstraints()
    {
        // Use high constraint rate to ensure some are generated
        var opts = SmallOptions with { InstructorConstraintRate = 1.0 };
        var gen = new AnthroDispatchMockDataGenerator();
        var dataset = await gen.GenerateAsync(opts);

        dataset.InstructorConstraints.Count.Should().BeGreaterThan(0);
        dataset.InstructorConstraints.Should().AllSatisfy(c =>
            c.InstructorId.Should().NotBeEmpty());
    }

    [Test]
    public async Task AnthroDispatchGenerator_ShouldCreateAgeAndHealthProfiles()
    {
        var opts = SmallOptions with { HealthLimitationRate = 1.0 };
        var gen = new AnthroDispatchMockDataGenerator();
        var dataset = await gen.GenerateAsync(opts);

        // All groups should have a valid average age
        dataset.Groups.Should().AllSatisfy(g =>
            g.AverageAge.Should().BeGreaterThan(0));

        // All instructors should have a valid age
        dataset.Instructors.Should().AllSatisfy(i =>
            i.Age.Should().BeGreaterThan(0));

        // Health limitations generated with rate 1.0
        dataset.HealthLimitations.Count.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task AnthroDispatchGenerator_ShouldBeDeterministicForSameSeed()
    {
        var gen = new AnthroDispatchMockDataGenerator();
        var ds1 = await gen.GenerateAsync(SmallOptions);
        var ds2 = await gen.GenerateAsync(SmallOptions);

        ds1.Groups.Count.Should().Be(ds2.Groups.Count);
        ds1.Instructors.Count.Should().Be(ds2.Instructors.Count);
        ds1.CurriculumPlans.Count.Should().Be(ds2.CurriculumPlans.Count);

        for (var i = 0; i < ds1.Instructors.Count; i++)
            ds1.Instructors[i].FullName.Should().Be(ds2.Instructors[i].FullName);
    }
}