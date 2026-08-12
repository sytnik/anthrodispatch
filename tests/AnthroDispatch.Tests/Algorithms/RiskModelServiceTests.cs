using AnthroDispatch.Application.Algorithms.Explanation;
using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.Metrics;
using AnthroDispatch.Domain.ValueObjects;
using FluentAssertions;

namespace AnthroDispatch.Tests.Algorithms;

[TestFixture]
public class RiskModelServiceTests
{
    [Test]
    public void Calculate_PerfectMetricsWithFullStability_ShouldReturnZero()
    {
        var metrics = new TimetableMetrics { FTech = 1.0, FPsych = 1.0, CInterf = 0.0 };

        var risk = RiskModelService.Calculate(metrics, fStable: 1.0);

        risk.Should().Be(0.0);
    }

    [Test]
    public void Calculate_WorstMetricsWithNoStability_ShouldReturnOne()
    {
        var metrics = new TimetableMetrics { FTech = 0.0, FPsych = 0.0, CInterf = 1.0 };

        var risk = RiskModelService.Calculate(metrics, fStable: 0.0);

        // delta1 + delta2 + delta3 + delta4 = 0.30 + 0.30 + 0.25 + 0.15 = 1.0
        risk.Should().BeApproximately(1.0, 1e-9);
    }

    [Test]
    public void Calculate_WithoutFStable_ShouldTreatChangeRiskAsZero()
    {
        var metrics = new TimetableMetrics { FTech = 0.0, FPsych = 0.0, CInterf = 1.0 };

        var risk = RiskModelService.Calculate(metrics, fStable: null);

        // Same as worst-case minus the delta4 * Rchange contribution (0.15).
        risk.Should().BeApproximately(1.0 - 0.15, 1e-9);
    }

    [Test]
    public void Calculate_PerfectFTech_ShouldContributeZeroConflictRisk()
    {
        // FTech < 1.0 is what triggers Rconflict; FTech == 1.0 must zero it out
        // regardless of how far below 1.0 it could have been.
        var perfectTech = new TimetableMetrics { FTech = 1.0, FPsych = 0.0, CInterf = 0.0 };
        var imperfectTech = new TimetableMetrics { FTech = 0.5, FPsych = 0.0, CInterf = 0.0 };

        var riskPerfect = RiskModelService.Calculate(perfectTech, fStable: 1.0);
        var riskImperfect = RiskModelService.Calculate(imperfectTech, fStable: 1.0);

        riskImperfect.Should().BeGreaterThan(riskPerfect, "an imperfect FTech must add conflict risk");
    }

    [Test]
    public void Calculate_HigherInterference_ShouldIncreaseCognitiveRisk()
    {
        // FCogn intentionally left equal on both sides: Risk_cognitive must
        // track CInterf (negative pairs only), not the FCogn synergy score.
        var lowInterference = new TimetableMetrics { FTech = 1.0, FPsych = 1.0, FCogn = 0.6, CInterf = 0.1 };
        var highInterference = new TimetableMetrics { FTech = 1.0, FPsych = 1.0, FCogn = 0.6, CInterf = 0.6 };

        var riskLow = RiskModelService.Calculate(lowInterference, fStable: 1.0);
        var riskHigh = RiskModelService.Calculate(highInterference, fStable: 1.0);

        riskHigh.Should().BeGreaterThan(riskLow);
    }

    [Test]
    public void Calculate_ArbitraryMetrics_ShouldMatchWeightedFormula()
    {
        var metrics = new TimetableMetrics { FTech = 0.9, FPsych = 0.6, CInterf = 0.3 };
        const double fStable = 0.8;

        var risk = RiskModelService.Calculate(metrics, fStable);

        var rConflict = 1.0 - 0.9;
        var rStress = 1.0 - 0.6;
        var rCognitive = 0.3; // Risk_cognitive = C_interf(x), read directly off metrics
        var rChange = 1.0 - fStable;
        var expected = 0.30 * rConflict + 0.30 * rStress + 0.25 * rCognitive + 0.15 * rChange;

        risk.Should().BeApproximately(expected, 1e-9);
    }

    [Test]
    public void FStable_IdenticalTimetables_ShouldReturnOne()
    {
        var groupId = Guid.NewGuid();
        var current = new Timetable
        {
            Classes = [new ScheduledClass { GroupId = groupId, Slot = new TimeSlot(1, 1) }]
        };
        var previous = current.DeepClone();

        var fStable = RiskModelService.FStable(current, previous);

        fStable.Should().Be(1.0);
    }

    [Test]
    public void FStable_AllSlotsChanged_ShouldReturnZero()
    {
        var id = Guid.NewGuid();
        var previous = new Timetable
        {
            Classes = [new ScheduledClass { Id = id, Slot = new TimeSlot(1, 1) }]
        };
        var current = new Timetable
        {
            Classes = [new ScheduledClass { Id = id, Slot = new TimeSlot(2, 3) }]
        };

        var fStable = RiskModelService.FStable(current, previous);

        fStable.Should().Be(0.0);
    }

    [Test]
    public void FStable_PartialChange_ShouldReturnProportionalValue()
    {
        var unchangedId = Guid.NewGuid();
        var changedId = Guid.NewGuid();
        var previous = new Timetable
        {
            Classes =
            [
                new ScheduledClass { Id = unchangedId, Slot = new TimeSlot(1, 1) },
                new ScheduledClass { Id = changedId, Slot = new TimeSlot(1, 2) }
            ]
        };
        var current = new Timetable
        {
            Classes =
            [
                new ScheduledClass { Id = unchangedId, Slot = new TimeSlot(1, 1) },
                new ScheduledClass { Id = changedId, Slot = new TimeSlot(3, 4) }
            ]
        };

        var fStable = RiskModelService.FStable(current, previous);

        fStable.Should().Be(0.5); // 1 of 2 classes changed slot
    }

    [Test]
    public void FStable_ClassNotPresentInPrevious_ShouldCountAsUnchanged()
    {
        // A class with no matching Id in `previous` never satisfies the
        // TryGetValue guard in FStable, so it cannot be counted as "changed" —
        // only classes present in both timetables with a different slot are.
        var previous = new Timetable { Classes = [] };
        var current = new Timetable
        {
            Classes = [new ScheduledClass { Id = Guid.NewGuid(), Slot = new TimeSlot(1, 1) }]
        };

        var fStable = RiskModelService.FStable(current, previous);

        fStable.Should().Be(1.0);
    }

    [Test]
    public void FStable_EmptyCurrentTimetable_ShouldReturnOne()
    {
        var current = new Timetable { Classes = [] };
        var previous = new Timetable { Classes = [] };

        var fStable = RiskModelService.FStable(current, previous);

        fStable.Should().Be(1.0);
    }
}
