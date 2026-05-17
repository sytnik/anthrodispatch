using AnthroDispatch.Application.Algorithms.Objective;
using AnthroDispatch.Domain.Enums;
using FluentAssertions;

namespace AnthroDispatch.Tests.Algorithms;

[TestFixture]
public class CircadianActivityTests
{
    [TestCase(ChronotypeCategory.DefiniteMorning, 2)]
    [TestCase(ChronotypeCategory.ModerateMorning, 3)]
    [TestCase(ChronotypeCategory.Intermediate, 4)]
    [TestCase(ChronotypeCategory.ModerateEvening, 6)]
    [TestCase(ChronotypeCategory.DefiniteEvening, 7)]
    public void CircadianActivity_ShouldPeakAtConfiguredChronotypeSlot(ChronotypeCategory chronotype, int peakPeriod)
    {
        var peakValue = CircadianActivityCalculator.Calculate(chronotype, peakPeriod);

        for (var period = 1; period <= 8; period++)
        {
            if (period == peakPeriod) continue;
            var other = CircadianActivityCalculator.Calculate(chronotype, period);
            peakValue.Should().BeGreaterThanOrEqualTo(other,
                $"peak at period {peakPeriod} should dominate period {period}");
        }
    }

    [Test]
    public void CircadianActivity_ShouldReturnValueBetweenZeroAndOne()
    {
        foreach (var cat in Enum.GetValues<ChronotypeCategory>())
            for (var p = 1; p <= 8; p++)
            {
                var v = CircadianActivityCalculator.Calculate(cat, p);
                v.Should().BeInRange(0.0, 1.0);
            }
    }
}