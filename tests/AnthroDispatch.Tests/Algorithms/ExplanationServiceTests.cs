using AnthroDispatch.Application.Algorithms.Explanation;
using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.Enums;
using AnthroDispatch.Domain.Metrics;
using AnthroDispatch.Domain.ValueObjects;
using FluentAssertions;

namespace AnthroDispatch.Tests.Algorithms;

[TestFixture]
public class ExplanationServiceTests
{
    private static AcademicGroup MakeGroup(ChronotypeCategory chronotype = ChronotypeCategory.DefiniteMorning) =>
        new() { Id = Guid.NewGuid(), Code = "G1", Chronotype = chronotype };

    private static Instructor MakeInstructor(ChronotypeCategory chronotype = ChronotypeCategory.DefiniteMorning,
        int maxClassesPerDay = 4) =>
        new() { Id = Guid.NewGuid(), FullName = "Instructor", Chronotype = chronotype, MaxClassesPerDay = maxClassesPerDay };

    private static Discipline MakeDiscipline(string name) => new() { Id = Guid.NewGuid(), Code = name, Name = name };

    [Test]
    public void ExplainClass_UnknownClassId_ShouldReturnEmptyExplanation()
    {
        var service = new ExplanationService([], [], []);
        var timetable = new Timetable();

        var explanation = service.ExplainClass(timetable, Guid.NewGuid());

        explanation.Reasons.Should().BeEmpty();
        explanation.ComponentScores.Should().BeEmpty();
        explanation.TradeOffs.Should().BeEmpty();
    }

    [Test]
    public void ExplainClass_NoConflicts_ShouldStateNoHardConflicts()
    {
        var group = MakeGroup();
        var instructor = MakeInstructor();
        var discipline = MakeDiscipline("Math");
        var sc = new ScheduledClass
        {
            GroupId = group.Id, InstructorId = instructor.Id, DisciplineId = discipline.Id, Slot = new TimeSlot(1, 2)
        };
        var timetable = new Timetable { Classes = [sc] };
        var service = new ExplanationService([group], [instructor], [discipline]);

        var explanation = service.ExplainClass(timetable, sc.Id);

        explanation.Reasons.Should().Contain("No hard conflicts detected for this slot.");
    }

    [Test]
    public void ExplainClass_GroupDoubleBooking_ShouldWarn()
    {
        var group = MakeGroup();
        var instructorA = MakeInstructor();
        var instructorB = MakeInstructor();
        var discipline = MakeDiscipline("Math");
        var slot = new TimeSlot(1, 2);
        var sc = new ScheduledClass
        {
            GroupId = group.Id, InstructorId = instructorA.Id, DisciplineId = discipline.Id, Slot = slot
        };
        var clash = new ScheduledClass
        {
            GroupId = group.Id, InstructorId = instructorB.Id, DisciplineId = discipline.Id, Slot = slot
        };
        var timetable = new Timetable { Classes = [sc, clash] };
        var service = new ExplanationService([group], [instructorA, instructorB], [discipline]);

        var explanation = service.ExplainClass(timetable, sc.Id);

        explanation.Reasons.Should().Contain("WARNING: Group double-booking detected.");
        explanation.Reasons.Should().NotContain("No hard conflicts detected for this slot.");
    }

    [Test]
    public void ExplainClass_InstructorDoubleBooking_ShouldWarn()
    {
        var groupA = MakeGroup();
        var groupB = MakeGroup();
        var instructor = MakeInstructor();
        var discipline = MakeDiscipline("Math");
        var slot = new TimeSlot(1, 2);
        var sc = new ScheduledClass
        {
            GroupId = groupA.Id, InstructorId = instructor.Id, DisciplineId = discipline.Id, Slot = slot
        };
        var clash = new ScheduledClass
        {
            GroupId = groupB.Id, InstructorId = instructor.Id, DisciplineId = discipline.Id, Slot = slot
        };
        var timetable = new Timetable { Classes = [sc, clash] };
        var service = new ExplanationService([groupA, groupB], [instructor], [discipline]);

        var explanation = service.ExplainClass(timetable, sc.Id);

        explanation.Reasons.Should().Contain("WARNING: Instructor double-booking detected.");
    }

    [Test]
    public void ExplainClass_PreviousDisciplineInSameDay_ShouldReportCognitiveCompatibility()
    {
        var group = MakeGroup();
        var instructor = MakeInstructor();
        var d1 = MakeDiscipline("Analysis");
        var d2 = MakeDiscipline("Painting");
        var first = new ScheduledClass
        {
            GroupId = group.Id, InstructorId = instructor.Id, DisciplineId = d1.Id, Slot = new TimeSlot(1, 1)
        };
        var second = new ScheduledClass
        {
            GroupId = group.Id, InstructorId = instructor.Id, DisciplineId = d2.Id, Slot = new TimeSlot(1, 2)
        };
        var compatibility = new CognitiveCompatibility { FromDisciplineId = d1.Id, ToDisciplineId = d2.Id, Score = -0.42 };
        var timetable = new Timetable { Classes = [first, second] };
        var service = new ExplanationService([group], [instructor], [d1, d2], [compatibility]);

        var explanation = service.ExplainClass(timetable, second.Id);

        explanation.Reasons.Should().Contain(r => r.Contains("cognitive compatibility -0.42"));
    }

    [Test]
    public void ExplainClass_FirstClassOfDay_ShouldNotReportCompatibility()
    {
        var group = MakeGroup();
        var instructor = MakeInstructor();
        var discipline = MakeDiscipline("Math");
        var sc = new ScheduledClass
        {
            GroupId = group.Id, InstructorId = instructor.Id, DisciplineId = discipline.Id, Slot = new TimeSlot(1, 1)
        };
        var timetable = new Timetable { Classes = [sc] };
        var service = new ExplanationService([group], [instructor], [discipline]);

        var explanation = service.ExplainClass(timetable, sc.Id);

        explanation.Reasons.Should().NotContain(r => r.Contains("cognitive compatibility"));
    }

    [Test]
    public void ExplainClass_PeriodBelowMax_ShouldIncludeTradeOff()
    {
        var group = MakeGroup();
        var instructor = MakeInstructor();
        var discipline = MakeDiscipline("Math");
        var sc = new ScheduledClass
        {
            GroupId = group.Id, InstructorId = instructor.Id, DisciplineId = discipline.Id, Slot = new TimeSlot(1, 3)
        };
        var timetable = new Timetable { Classes = [sc] };
        var service = new ExplanationService([group], [instructor], [discipline]);

        var explanation = service.ExplainClass(timetable, sc.Id);

        explanation.TradeOffs.Should().ContainSingle();
    }

    [Test]
    public void ExplainClass_LastPeriodOfDay_ShouldNotIncludeTradeOff()
    {
        var group = MakeGroup();
        var instructor = MakeInstructor();
        var discipline = MakeDiscipline("Math");
        var sc = new ScheduledClass
        {
            GroupId = group.Id, InstructorId = instructor.Id, DisciplineId = discipline.Id, Slot = new TimeSlot(1, 8)
        };
        var timetable = new Timetable { Classes = [sc] };
        var service = new ExplanationService([group], [instructor], [discipline]);

        var explanation = service.ExplainClass(timetable, sc.Id);

        explanation.TradeOffs.Should().BeEmpty();
    }

    [Test]
    public void ExplainClass_Scores_ShouldBlendGroupAndInstructorActivity60_40()
    {
        // DefiniteMorning peaks at period 2 (activity = 1.0 exactly there).
        var group = MakeGroup(ChronotypeCategory.DefiniteMorning);
        var instructor = MakeInstructor(ChronotypeCategory.DefiniteEvening); // peaks at period 7
        var discipline = MakeDiscipline("Math");
        var sc = new ScheduledClass
        {
            GroupId = group.Id, InstructorId = instructor.Id, DisciplineId = discipline.Id, Slot = new TimeSlot(1, 2)
        };
        var timetable = new Timetable { Classes = [sc] };
        var service = new ExplanationService([group], [instructor], [discipline]);

        var explanation = service.ExplainClass(timetable, sc.Id);

        explanation.ComponentScores["FCircGroup"].Should().Be(1.0);
        explanation.ComponentScores["FCircBlended"].Should()
            .BeApproximately(0.6 * explanation.ComponentScores["FCircGroup"] + 0.4 * explanation.ComponentScores["FCircInstructor"], 1e-9);
    }

    [Test]
    public void ExplainTimetable_NullMetrics_ShouldNotAddComponentEntries()
    {
        var timetable = new Timetable();
        var service = new ExplanationService([], [], []);

        var explanation = service.ExplainTimetable(timetable);

        explanation.ComponentScores.Should().BeEmpty();
        explanation.Strengths.Should().NotContain(s => s.Contains("Strongest component"));
        explanation.Weaknesses.Should().NotContain(s => s.Contains("Weakest component"));
    }

    [Test]
    public void ExplainTimetable_IdentifiesStrongestAndWeakestComponent()
    {
        var timetable = new Timetable
        {
            Metrics = new TimetableMetrics { FTech = 0.95, FCirc = 0.40, FPsych = 0.80, FCogn = 0.70 }
        };
        var service = new ExplanationService([], [], []);

        var explanation = service.ExplainTimetable(timetable);

        explanation.Strengths.Should().Contain(s => s.Contains("Strongest component: FTech"));
        explanation.Weaknesses.Should().Contain(s => s.Contains("Weakest component: FCirc"));
    }

    [Test]
    public void ExplainTimetable_NoConflicts_ShouldAddStrength()
    {
        var timetable = new Timetable
        {
            Metrics = new TimetableMetrics { FTech = 1.0, FCirc = 1.0, FPsych = 1.0, FCogn = 1.0, Conflicts = 0 }
        };
        var service = new ExplanationService([], [], []);

        var explanation = service.ExplainTimetable(timetable);

        explanation.Strengths.Should().Contain("No scheduling conflicts detected.");
    }

    [Test]
    public void ExplainTimetable_WithConflicts_ShouldReportCount()
    {
        var timetable = new Timetable
        {
            Metrics = new TimetableMetrics { FTech = 0.5, FCirc = 0.5, FPsych = 0.5, FCogn = 0.5, Conflicts = 3 }
        };
        var service = new ExplanationService([], [], []);

        var explanation = service.ExplainTimetable(timetable);

        explanation.Weaknesses.Should().Contain("3 hard conflict(s) remain.");
    }

    [Test]
    public void ExplainTimetable_HighMismatchGroup_ShouldBeReportedAndRecommended()
    {
        var group = MakeGroup(ChronotypeCategory.DefiniteMorning); // peak = period 2
        var discipline = MakeDiscipline("Math");
        // Period 8 is far from the peak (period 2) — near-total circadian mismatch.
        var sc = new ScheduledClass { GroupId = group.Id, DisciplineId = discipline.Id, Slot = new TimeSlot(1, 8) };
        var timetable = new Timetable { Classes = [sc] };
        var service = new ExplanationService([group], [], [discipline]);

        var explanation = service.ExplainTimetable(timetable);

        explanation.Weaknesses.Should().Contain(w => w.Contains("Highest-risk groups"));
        explanation.Recommendations.Should().Contain(r => r.Contains(group.Code) && r.Contains("severe circadian mismatch"));
    }

    [Test]
    public void ExplainTimetable_WorstCognitiveSequence_ShouldBeReported()
    {
        var group = MakeGroup();
        var d1 = MakeDiscipline("Analysis");
        var d2 = MakeDiscipline("Painting");
        var first = new ScheduledClass { GroupId = group.Id, DisciplineId = d1.Id, Slot = new TimeSlot(1, 1) };
        var second = new ScheduledClass { GroupId = group.Id, DisciplineId = d2.Id, Slot = new TimeSlot(1, 2) };
        var compatibility = new CognitiveCompatibility { FromDisciplineId = d1.Id, ToDisciplineId = d2.Id, Score = -0.5 };
        var timetable = new Timetable { Classes = [first, second] };
        var service = new ExplanationService([group], [], [d1, d2], [compatibility]);

        var explanation = service.ExplainTimetable(timetable);

        explanation.Weaknesses.Should().Contain(w => w.Contains("Worst cognitive sequences"));
        explanation.Recommendations.Should().Contain("Improve cognitive sequencing by pairing complementary subjects.");
    }

    [Test]
    public void ExplainTimetable_GoodCognitiveSequence_ShouldNotBeFlagged()
    {
        var group = MakeGroup();
        var d1 = MakeDiscipline("Analysis");
        var d2 = MakeDiscipline("Statistics");
        var first = new ScheduledClass { GroupId = group.Id, DisciplineId = d1.Id, Slot = new TimeSlot(1, 1) };
        var second = new ScheduledClass { GroupId = group.Id, DisciplineId = d2.Id, Slot = new TimeSlot(1, 2) };
        var compatibility = new CognitiveCompatibility { FromDisciplineId = d1.Id, ToDisciplineId = d2.Id, Score = 0.6 };
        var timetable = new Timetable { Classes = [first, second] };
        var service = new ExplanationService([group], [], [d1, d2], [compatibility]);

        var explanation = service.ExplainTimetable(timetable);

        explanation.Weaknesses.Should().NotContain(w => w.Contains("Worst cognitive sequences"));
    }

    [Test]
    public void ExplainTimetable_OverloadedInstructor_ShouldBeReported()
    {
        var group = MakeGroup();
        var instructor = MakeInstructor(maxClassesPerDay: 2);
        var discipline = MakeDiscipline("Math");
        var classes = Enumerable.Range(1, 3)
            .Select(period => new ScheduledClass
            {
                GroupId = group.Id, InstructorId = instructor.Id, DisciplineId = discipline.Id,
                Slot = new TimeSlot(1, period)
            })
            .ToList();
        var timetable = new Timetable { Classes = classes };
        var service = new ExplanationService([group], [instructor], [discipline]);

        var explanation = service.ExplainTimetable(timetable);

        explanation.Weaknesses.Should().Contain(w => w.Contains("Overloaded instructors") && w.Contains(instructor.FullName));
        explanation.Recommendations.Should().Contain("Redistribute instructor workload to avoid excessive daily class loads.");
    }

    [Test]
    public void ExplainTimetable_InstructorWithinLimit_ShouldNotBeFlagged()
    {
        var group = MakeGroup();
        var instructor = MakeInstructor(maxClassesPerDay: 3);
        var discipline = MakeDiscipline("Math");
        var classes = Enumerable.Range(1, 3)
            .Select(period => new ScheduledClass
            {
                GroupId = group.Id, InstructorId = instructor.Id, DisciplineId = discipline.Id,
                Slot = new TimeSlot(1, period)
            })
            .ToList();
        var timetable = new Timetable { Classes = classes };
        var service = new ExplanationService([group], [instructor], [discipline]);

        var explanation = service.ExplainTimetable(timetable);

        explanation.Weaknesses.Should().NotContain(w => w.Contains("Overloaded instructors"));
    }

    [TestCase(0.5, 0.9, 0.9, "Consider rescheduling classes for groups with evening chronotype earlier in the day.")]
    [TestCase(0.9, 0.5, 0.9, "Reduce consecutive high-load sessions to improve psychological comfort.")]
    [TestCase(0.9, 0.9, 0.5, "Improve cognitive sequencing by pairing complementary subjects.")]
    public void ExplainTimetable_LowComponent_ShouldTriggerMatchingRecommendation(
        double fCirc, double fPsych, double fCogn, string expectedRecommendation)
    {
        var timetable = new Timetable
        {
            Metrics = new TimetableMetrics { FTech = 0.9, FCirc = fCirc, FPsych = fPsych, FCogn = fCogn }
        };
        var service = new ExplanationService([], [], []);

        var explanation = service.ExplainTimetable(timetable);

        explanation.Recommendations.Should().Contain(expectedRecommendation);
    }

    [Test]
    public void ComputeExplainability_AllClassesConflictFree_ShouldReturnOne()
    {
        var group = MakeGroup();
        var discipline = MakeDiscipline("Math");
        var sc = new ScheduledClass { GroupId = group.Id, DisciplineId = discipline.Id, Slot = new TimeSlot(1, 2) };
        var timetable = new Timetable { Classes = [sc] };
        var service = new ExplanationService([group], [], [discipline]);

        service.ComputeExplainability(timetable).Should().Be(1.0);
    }

    [Test]
    public void ComputeExplainability_MixedClasses_ShouldReturnPartialFraction()
    {
        var group = MakeGroup(ChronotypeCategory.DefiniteMorning); // peak = period 2
        var discipline = MakeDiscipline("Math");
        // Good class: at the chronotype peak, no conflicts.
        var good = new ScheduledClass { GroupId = group.Id, DisciplineId = discipline.Id, Slot = new TimeSlot(1, 2) };
        // Bad class: far from peak (low blended activity) and double-booked with
        // itself's own group/slot pair via a clashing sibling class.
        var bad = new ScheduledClass { GroupId = group.Id, DisciplineId = discipline.Id, Slot = new TimeSlot(2, 8) };
        var clash = new ScheduledClass { GroupId = group.Id, DisciplineId = discipline.Id, Slot = new TimeSlot(2, 8) };
        var timetable = new Timetable { Classes = [good, bad, clash] };
        var service = new ExplanationService([group], [], [discipline]);

        var explainability = service.ComputeExplainability(timetable);

        explainability.Should().BeInRange(0.0, 1.0);
        explainability.Should().BeLessThan(1.0, "the clashing low-activity pair should not count as a positive contribution");
    }
}
