using AnthroDispatch.Application.Algorithms.Sra;
using AnthroDispatch.Domain.Metrics;
using FluentAssertions;

namespace AnthroDispatch.Tests.Algorithms;

[TestFixture]
public class SraServiceTests
{
    private static List<TimetableMetrics> BuildWellConditionedSamples(int count, int seed)
    {
        var rng = new Random(seed);
        return Enumerable.Range(0, count)
            .Select(_ => new TimetableMetrics
            {
                FTech = rng.NextDouble(),
                FCirc = rng.NextDouble(),
                FPsych = rng.NextDouble(),
                FCogn = rng.NextDouble()
            })
            .ToList();
    }

    [Test]
    public void Adapt_SmallSample_ProducesValidSimplexWeightsViaRidge()
    {
        var svc = new SraService();
        var oldWeights = ObjectiveWeights.Default;
        var samples = BuildWellConditionedSamples(20, seed: 7); // < 50 => ridge path

        var result = svc.Adapt(samples, oldWeights);

        double.IsNaN(result.NewWeights.Tech).Should().BeFalse();
        double.IsNaN(result.NewWeights.Circ).Should().BeFalse();
        double.IsNaN(result.NewWeights.Psych).Should().BeFalse();
        double.IsNaN(result.NewWeights.Cogn).Should().BeFalse();

        var sum = result.NewWeights.Tech + result.NewWeights.Circ + result.NewWeights.Psych + result.NewWeights.Cogn;
        sum.Should().BeApproximately(1.0, 1e-6);
        result.NewWeights.Tech.Should().BeGreaterThanOrEqualTo(0.05 - 1e-9);
        result.NewWeights.Circ.Should().BeGreaterThanOrEqualTo(0.05 - 1e-9);
        result.NewWeights.Psych.Should().BeGreaterThanOrEqualTo(0.05 - 1e-9);
        result.NewWeights.Cogn.Should().BeGreaterThanOrEqualTo(0.05 - 1e-9);
    }

    [Test]
    public void Adapt_LargeSample_ProducesValidSimplexWeightsViaPlainOls()
    {
        var svc = new SraService();
        var oldWeights = ObjectiveWeights.Default;
        var samples = BuildWellConditionedSamples(60, seed: 7); // >= 50 => plain OLS path

        var result = svc.Adapt(samples, oldWeights);

        double.IsNaN(result.NewWeights.Tech).Should().BeFalse();
        var sum = result.NewWeights.Tech + result.NewWeights.Circ + result.NewWeights.Psych + result.NewWeights.Cogn;
        sum.Should().BeApproximately(1.0, 1e-6);
    }

    [Test]
    public void Adapt_RidgeMatrixIsWellConditioned_EvenWithNearCollinearFeatures()
    {
        // Features that are strongly (but not perfectly) correlated make plain
        // OLS numerically fragile on a small sample. Ridge (N < 50) must still
        // resolve to a finite, valid simplex instead of blowing up.
        var svc = new SraService();
        var oldWeights = ObjectiveWeights.Default;
        var rng = new Random(3);
        var samples = Enumerable.Range(0, 12)
            .Select(_ =>
            {
                var baseValue = rng.NextDouble();
                double Jitter() => Math.Clamp(baseValue + (rng.NextDouble() - 0.5) * 0.02, 0.0, 1.0);
                return new TimetableMetrics
                {
                    FTech = Jitter(), FCirc = Jitter(), FPsych = Jitter(), FCogn = Jitter()
                };
            })
            .ToList();

        var result = svc.Adapt(samples, oldWeights);

        double.IsNaN(result.NewWeights.Tech).Should().BeFalse();
        double.IsInfinity(result.NewWeights.Tech).Should().BeFalse();
        var sum = result.NewWeights.Tech + result.NewWeights.Circ + result.NewWeights.Psych + result.NewWeights.Cogn;
        sum.Should().BeApproximately(1.0, 1e-6);
    }
}
