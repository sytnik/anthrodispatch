using AnthroDispatch.Application.Abstractions;
using AnthroDispatch.Application.Algorithms.Explanation;
using AnthroDispatch.Application.Algorithms.Genetic;
using AnthroDispatch.Application.Algorithms.Objective;
using AnthroDispatch.Application.Algorithms.Repair;
using AnthroDispatch.Application.Algorithms.ScoreIa;
using AnthroDispatch.Domain.Metrics;
using AnthroDispatch.Infrastructure.MockData;
using FluentAssertions;

namespace AnthroDispatch.Tests.Algorithms;

[TestFixture]
public class ScoreIaServiceTests
{
    private static async Task<(MockDatasetGenerator gen, DatasetGenerationResult dataset)> GetDataset()
    {
        var gen = new MockDatasetGenerator();
        var req = new DatasetGenerationRequest(42, 4, 80, 8, 8, 6);
        var dataset = await gen.GenerateAsync(req);
        return (gen, dataset);
    }

    [Test]
    public async Task AmdService_Run_ShouldPopulateTopCandidates_SortedDescendingByF()
    {
        var (_, ds) = await GetDataset();
        var objFn = new ObjectiveFunctionService(ds.Groups, ds.Instructors, ds.Disciplines, ds.Rooms, ds.Assignments,
            ds.Compatibilities);
        var repair = new RepairService(ds.Rooms, ds.Instructors);
        var opts = GaOptions.FastDev with { Seed = 42, TopMCandidates = 5 };

        var result = new AmdService(ds.Groups, ds.Instructors, ds.Disciplines, ds.Rooms, ds.Assignments,
                ds.Compatibilities, objFn, repair, opts)
            .Run(ObjectiveWeights.Default);

        result.TopCandidates.Should().NotBeNull();
        result.TopCandidates!.Should().HaveCountLessThanOrEqualTo(opts.TopMCandidates);
        result.TopCandidates![0].Id.Should().Be(result.BestTimetable.Id, "the best individual must head X_cand");
        for (var i = 1; i < result.TopCandidates!.Count; i++)
            result.TopCandidates[i - 1].Metrics!.F.Should().BeGreaterThanOrEqualTo(result.TopCandidates[i].Metrics!.F);
    }

    [Test]
    public async Task ScoreIaService_RankCandidates_ShouldReturnAllCandidatesSortedDescendingByScoreIa()
    {
        var (_, ds) = await GetDataset();
        var objFn = new ObjectiveFunctionService(ds.Groups, ds.Instructors, ds.Disciplines, ds.Rooms, ds.Assignments,
            ds.Compatibilities);
        var repair = new RepairService(ds.Rooms, ds.Instructors);
        var opts = GaOptions.FastDev with { Seed = 42, TopMCandidates = 5 };

        var result = new AmdService(ds.Groups, ds.Instructors, ds.Disciplines, ds.Rooms, ds.Assignments,
                ds.Compatibilities, objFn, repair, opts)
            .Run(ObjectiveWeights.Default);

        var explanation = new ExplanationService(ds.Groups, ds.Instructors, ds.Disciplines, ds.Compatibilities);
        var scoreIa = new ScoreIaService(explanation);

        var ranked = scoreIa.RankCandidates(result.TopCandidates!);

        ranked.Should().HaveCount(result.TopCandidates!.Count);
        for (var i = 1; i < ranked.Count; i++)
            ranked[i - 1].ScoreIa.Should().BeGreaterThanOrEqualTo(ranked[i].ScoreIa);
    }

    [Test]
    public async Task ScoreIaService_BuildZ_ShouldReturnComponentsMatchingCandidateMetrics()
    {
        var (_, ds) = await GetDataset();
        var objFn = new ObjectiveFunctionService(ds.Groups, ds.Instructors, ds.Disciplines, ds.Rooms, ds.Assignments,
            ds.Compatibilities);
        var repair = new RepairService(ds.Rooms, ds.Instructors);
        var core = new GaCore(ds.Groups, ds.Instructors, ds.Disciplines, ds.Rooms, ds.Assignments, ds.Compatibilities,
            objFn, repair, 42);
        var explanation = new ExplanationService(ds.Groups, ds.Instructors, ds.Disciplines, ds.Compatibilities);
        var scoreIa = new ScoreIaService(explanation);

        var candidate = core.RandomInitialize();
        objFn.Evaluate(candidate, ObjectiveWeights.Default);

        var z = scoreIa.BuildZ(candidate, previous: null);

        z.FTech.Should().Be(candidate.Metrics!.FTech);
        z.FCirc.Should().Be(candidate.Metrics!.FCirc);
        z.FPsych.Should().Be(candidate.Metrics!.FPsych);
        z.FCogn.Should().Be(candidate.Metrics!.FCogn);
        z.FStable.Should().Be(1.0, "with no previous approved version there is nothing to destabilise");
        z.Risk.Should().BeInRange(0.0, 1.0);
        z.Explainability.Should().BeInRange(0.0, 1.0);
    }

    [Test]
    public async Task ScoreIaService_BuildZ_WithIdenticalPrevious_ShouldReportFullStability()
    {
        var (_, ds) = await GetDataset();
        var objFn = new ObjectiveFunctionService(ds.Groups, ds.Instructors, ds.Disciplines, ds.Rooms, ds.Assignments,
            ds.Compatibilities);
        var repair = new RepairService(ds.Rooms, ds.Instructors);
        var core = new GaCore(ds.Groups, ds.Instructors, ds.Disciplines, ds.Rooms, ds.Assignments, ds.Compatibilities,
            objFn, repair, 42);
        var explanation = new ExplanationService(ds.Groups, ds.Instructors, ds.Disciplines, ds.Compatibilities);
        var scoreIa = new ScoreIaService(explanation);

        var candidate = core.RandomInitialize();
        objFn.Evaluate(candidate, ObjectiveWeights.Default);
        var previous = candidate.DeepClone();

        var z = scoreIa.BuildZ(candidate, previous);

        z.FStable.Should().Be(1.0);
    }

    [Test]
    public void ExplanationService_ComputeExplainability_ShouldReturnZero_WhenNoClasses()
    {
        var explanation = new ExplanationService([], [], []);
        var timetable = new AnthroDispatch.Domain.Entities.Timetable();

        explanation.ComputeExplainability(timetable).Should().Be(0.0);
    }
}
