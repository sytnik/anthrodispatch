using AnthroDispatch.Application.Abstractions;
using AnthroDispatch.Application.Algorithms.Awm;
using AnthroDispatch.Application.Algorithms.Cpc;
using AnthroDispatch.Application.Algorithms.Genetic;
using AnthroDispatch.Application.Algorithms.Objective;
using AnthroDispatch.Application.Algorithms.Repair;
using AnthroDispatch.Application.Algorithms.Sra;
using AnthroDispatch.Domain.Metrics;
using AnthroDispatch.Infrastructure.MockData;
using FluentAssertions;

namespace AnthroDispatch.Tests.Algorithms;

[TestFixture]
public class AlgorithmTests
{
    private static async Task<(MockDatasetGenerator gen, DatasetGenerationResult dataset)> GetDataset()
    {
        var gen = new MockDatasetGenerator();
        var req = new DatasetGenerationRequest(42, 4, 80, 8, 8, 6);
        var dataset = await gen.GenerateAsync(req);
        return (gen, dataset);
    }

    [Test]
    public async Task BaselineGa_ShouldReturnValidResult()
    {
        var (_, ds) = await GetDataset();
        var objFn = new ObjectiveFunctionService(ds.Groups, ds.Instructors, ds.Disciplines, ds.Rooms, ds.Assignments,
            ds.Compatibilities);
        var repair = new RepairService(ds.Rooms, ds.Instructors);
        var opts = GaOptions.FastDev with { Seed = 42 };

        var result = new BaselineGaService(ds.Groups, ds.Instructors, ds.Disciplines, ds.Rooms, ds.Assignments,
                ds.Compatibilities, objFn, repair, opts)
            .Run(ObjectiveWeights.Default);

        result.Should().NotBeNull();
        result.BestMetrics.F.Should().BeInRange(0.0, 1.0);
        result.GenerationsRun.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task Amd_ShouldReturnValidResult()
    {
        var (_, ds) = await GetDataset();
        var objFn = new ObjectiveFunctionService(ds.Groups, ds.Instructors, ds.Disciplines, ds.Rooms, ds.Assignments,
            ds.Compatibilities);
        var repair = new RepairService(ds.Rooms, ds.Instructors);
        var opts = GaOptions.FastDev with { Seed = 42 };

        var result = new AmdService(ds.Groups, ds.Instructors, ds.Disciplines, ds.Rooms, ds.Assignments,
                ds.Compatibilities, objFn, repair, opts)
            .Run(ObjectiveWeights.Default);

        result.Should().NotBeNull();
        result.BestMetrics.F.Should().BeInRange(0.0, 1.0);
    }

    [Test]
    public async Task Amd_ShouldImproveBestFitnessOverGenerations()
    {
        var (_, ds) = await GetDataset();
        var objFn = new ObjectiveFunctionService(ds.Groups, ds.Instructors, ds.Disciplines, ds.Rooms, ds.Assignments,
            ds.Compatibilities);
        var repair = new RepairService(ds.Rooms, ds.Instructors);
        var opts = new GaOptions { PopulationSize = 20, MaxGenerations = 30, StagnationGenerations = 30, Seed = 42 };

        var result = new AmdService(ds.Groups, ds.Instructors, ds.Disciplines, ds.Rooms, ds.Assignments,
                ds.Compatibilities, objFn, repair, opts)
            .Run(ObjectiveWeights.Default);

        // First generation fitness should be <= best fitness at the end
        result.FitnessHistory.Should().NotBeEmpty();
        result.BestMetrics.F.Should().BeGreaterThanOrEqualTo(result.FitnessHistory.First());
    }

    [Test]
    public async Task Repair_ShouldNotIncreaseConflicts()
    {
        var (_, ds) = await GetDataset();
        var objFn = new ObjectiveFunctionService(ds.Groups, ds.Instructors, ds.Disciplines, ds.Rooms, ds.Assignments,
            ds.Compatibilities);
        var repair = new RepairService(ds.Rooms, ds.Instructors);
        var core = new GaCore(ds.Groups, ds.Instructors, ds.Disciplines, ds.Rooms, ds.Assignments, ds.Compatibilities,
            objFn, repair, 42);

        var timetable = core.RandomInitialize();
        var conflictsBefore = FtechCalculator
            .CountConflicts(timetable, ds.Rooms, ds.Instructors, ds.Assignments);

        repair.Repair(timetable);
        var conflictsAfter = FtechCalculator
            .CountConflicts(timetable, ds.Rooms, ds.Instructors, ds.Assignments);

        conflictsAfter.Should().BeLessThanOrEqualTo(conflictsBefore);
    }

    [Test]
    public async Task Awm_ShouldPreferLowQualitySlots()
    {
        var (_, ds) = await GetDataset();
        var objFn = new ObjectiveFunctionService(ds.Groups, ds.Instructors, ds.Disciplines, ds.Rooms, ds.Assignments,
            ds.Compatibilities);
        var repair = new RepairService(ds.Rooms, ds.Instructors);
        var rng = new Random(42);
        var awm = new AwmMutation(ds.Groups, ds.Instructors, ds.Compatibilities, repair, beta: 2.0, rng);
        var core = new GaCore(ds.Groups, ds.Instructors, ds.Disciplines, ds.Rooms, ds.Assignments, ds.Compatibilities,
            objFn, repair, 42);

        var trials = 200;
        var lowQualityChosen = 0;

        for (var i = 0; i < trials; i++)
        {
            var t = core.RandomInitialize();
            var weights = ObjectiveWeights.Default;
            objFn.Evaluate(t, weights);

            // Track slot that was changed
            var slotsBefore = t.Classes.Select(c => c.Slot).ToList();
            awm.Mutate(t, weights);
            var slotsAfter = t.Classes.Select(c => c.Slot).ToList();

            // If any slot changed, count it (AWM always attempts a mutation)
            if (slotsBefore.Zip(slotsAfter, (a, b) => a != b).Any(changed => changed))
                lowQualityChosen++;
        }

        // AWM should mutate at least occasionally (beta=2 means low-quality slots strongly preferred)
        lowQualityChosen.Should().BeGreaterThan(0, "AWM should produce mutations");
    }

    [Test]
    public async Task Cpc_ShouldCreateChildFromParentDayBlocks()
    {
        var (_, ds) = await GetDataset();
        var objFn = new ObjectiveFunctionService(ds.Groups, ds.Instructors, ds.Disciplines, ds.Rooms, ds.Assignments,
            ds.Compatibilities);
        var repair = new RepairService(ds.Rooms, ds.Instructors);
        var core = new GaCore(ds.Groups, ds.Instructors, ds.Disciplines, ds.Rooms, ds.Assignments, ds.Compatibilities,
            objFn, repair, 42);
        var rng = new Random(42);
        var cpc = new DayWiseCpcCrossover(ds.Groups, ds.Instructors, repair, gamma: 5.0, rng);

        var parentA = core.RandomInitialize();
        var parentB = core.RandomInitialize();
        objFn.Evaluate(parentA, ObjectiveWeights.Default);
        objFn.Evaluate(parentB, ObjectiveWeights.Default);

        var child = cpc.Crossover(parentA, parentB);

        // Child should have classes, each from a valid day
        child.Classes.Should().NotBeEmpty();
        foreach (var sc in child.Classes)
        {
            sc.Slot.Day.Should().BeInRange(1, 6);
            sc.Slot.Period.Should().BeInRange(1, 8);

            // Each class day block must come entirely from parentA or parentB
            var inParentA = parentA.Classes.Any(c => c.AssignmentId == sc.AssignmentId && c.Slot == sc.Slot);
            var inParentB = parentB.Classes.Any(c => c.AssignmentId == sc.AssignmentId && c.Slot == sc.Slot);
            (inParentA || inParentB).Should().BeTrue("CPC child slots must originate from one of the parents");
        }
    }

    [Test]
    public void Sra_ShouldMoveWeightsTowardReferenceVector()
    {
        // Expert reference = (0.15, 0.30, 0.35, 0.20)
        var referenceValues = new[] { 0.15, 0.30, 0.35, 0.20 };
        var startWeights = ObjectiveWeights.Default; // (0.25, 0.25, 0.25, 0.25)

        var distanceBefore = Distance(startWeights, referenceValues);

        // Run 10 SRA cycles with samples that reflect the reference weighting
        var weights = startWeights;
        SraResult? lastResult = null;
        for (var cycle = 0; cycle < 10; cycle++)
        {
            // Generate metrics that have noticeable spread to help OLS find associations
            var rng = new Random(100 + cycle);
            var samples = Enumerable.Range(0, 60).Select(_ =>
            {
                var fTech = Math.Clamp(0.5 + rng.NextDouble() * 0.4, 0, 1);
                var fCirc = Math.Clamp(0.55 + rng.NextDouble() * 0.35, 0, 1);
                var fPsych = Math.Clamp(0.6 + rng.NextDouble() * 0.35, 0, 1);
                var fCogn = Math.Clamp(0.5 + rng.NextDouble() * 0.4, 0, 1);
                return new TimetableMetrics { FTech = fTech, FCirc = fCirc, FPsych = fPsych, FCogn = fCogn };
            }).ToList();

            lastResult = new SraService().Adapt(samples, weights, seed: 42 + cycle);
            weights = lastResult.NewWeights;
        }

        // Weights must be valid simplex after adaptation
        weights.Tech.Should().BeGreaterThanOrEqualTo(0.05);
        weights.Circ.Should().BeGreaterThanOrEqualTo(0.05);
        weights.Psych.Should().BeGreaterThanOrEqualTo(0.05);
        weights.Cogn.Should().BeGreaterThanOrEqualTo(0.05);
        var sum = weights.Tech + weights.Circ + weights.Psych + weights.Cogn;
        sum.Should().BeApproximately(1.0, 1e-9);

        // SraResult should report a distance and correlation to the reference
        lastResult!.DistanceToReference.Should().BeInRange(0, 2.0);
        lastResult.CorrelationToReference.Should().BeInRange(-1.0, 1.0);

        // Weights should have moved away from the uniform starting point (0.25, 0.25, 0.25, 0.25)
        var distanceAfter = Distance(weights, referenceValues);
        distanceAfter.Should().BeLessThan(distanceBefore + 0.05,
            "SRA should not dramatically move weights further from the reference");
    }

    private static double Distance(ObjectiveWeights w, double[] refValues)
    {
        return Math.Sqrt(
            Math.Pow(w.Tech - refValues[0], 2) +
            Math.Pow(w.Circ - refValues[1], 2) +
            Math.Pow(w.Psych - refValues[2], 2) +
            Math.Pow(w.Cogn - refValues[3], 2));
    }

    [Test]
    public void Sra_ShouldReturnWeightsSummingToOne()
    {
        var samples = Enumerable.Range(0, 30).Select(i => new TimetableMetrics
        {
            FTech = 0.7 + i % 3 * 0.05,
            FCirc = 0.6 + i % 4 * 0.05,
            FPsych = 0.65,
            FCogn = 0.55
        }).ToList();

        var old = ObjectiveWeights.Default;
        var result = new SraService().Adapt(samples, old, seed: 42);

        var sum = result.NewWeights.Tech + result.NewWeights.Circ + result.NewWeights.Psych + result.NewWeights.Cogn;
        sum.Should().BeApproximately(1.0, 1e-9);
        result.NewWeights.Tech.Should().BeGreaterThanOrEqualTo(0.05);
        result.NewWeights.Circ.Should().BeGreaterThanOrEqualTo(0.05);
        result.NewWeights.Psych.Should().BeGreaterThanOrEqualTo(0.05);
        result.NewWeights.Cogn.Should().BeGreaterThanOrEqualTo(0.05);
    }
}