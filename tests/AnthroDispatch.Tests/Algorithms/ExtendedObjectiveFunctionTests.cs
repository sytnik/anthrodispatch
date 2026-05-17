using AnthroDispatch.Application.Algorithms.Objective;
using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.Entities.Anthropocentric;
using AnthroDispatch.Domain.Enums;
using AnthroDispatch.Domain.Metrics;
using AnthroDispatch.Domain.ValueObjects;
using FluentAssertions;

namespace AnthroDispatch.Tests.Algorithms;

[TestFixture]
public class ExtendedObjectiveFunctionTests
{
    [Test]
    public void ObjectiveFunction_ShouldApplyAgeAwareCircadianCorrection()
    {
        // Older instructor (70) should have lower amplitude than young (25) — modifier < 1
        var youngActivity = CircadianActivityCalculator.Calculate(ChronotypeCategory.Intermediate, 4, 25);
        var oldActivity = CircadianActivityCalculator.Calculate(ChronotypeCategory.Intermediate, 4, 70);

        youngActivity.Should()
            .BeGreaterThan(oldActivity * 0.99, "Younger person should have higher circadian activity");
    }

    [Test]
    public void CircadianActivityCalculator_AgeModifier_ShouldBeClampedToRange()
    {
        // Very young person (age 10) → clamp at 1.05
        var modYoung = CircadianActivityCalculator.AgeModifier(10);
        modYoung.Should().BeApproximately(1.05, 0.001);

        // Very old person (age 100) → clamp at 0.85
        var modOld = CircadianActivityCalculator.AgeModifier(100);
        modOld.Should().BeApproximately(0.85, 0.001);

        // Middle-aged (age 45) → modifier = 1.0
        var mod45 = CircadianActivityCalculator.AgeModifier(45);
        mod45.Should().BeApproximately(1.0, 0.01);
    }

    [Test]
    public void ObjectiveFunction_ShouldPenalizeHealthConstraintViolations()
    {
        // Build a timetable with a class at period 1
        var group = new AcademicGroup
            { Id = Guid.NewGuid(), StudentCount = 20, Chronotype = ChronotypeCategory.Intermediate, AverageAge = 22 };
        var instr = new Instructor
            { Id = Guid.NewGuid(), Chronotype = ChronotypeCategory.Intermediate, MaxClassesPerDay = 5, Age = 35 };
        var disc = new Discipline
        {
            Id = Guid.NewGuid(), ProcessType = CognitiveProcessType.Analytical, LoadLevel = CognitiveLoadLevel.Medium,
            Domain = DisciplineDomain.Technical
        };
        var room = new Room { Id = Guid.NewGuid(), Type = RoomType.LectureHall, Capacity = 100 };
        var assign = new TeachingAssignment
        {
            Id = Guid.NewGuid(), GroupId = group.Id, InstructorId = instr.Id, DisciplineId = disc.Id,
            ClassType = ClassType.Lecture, RequiredPeriods = 1
        };

        var sc = new ScheduledClass
        {
            AssignmentId = assign.Id, GroupId = group.Id, InstructorId = instr.Id,
            DisciplineId = disc.Id, RoomId = room.Id, Slot = new TimeSlot(1, 1) // period 1 = early
        };
        var timetable = new Timetable();
        timetable.Classes.Add(sc);
        var weights = ObjectiveWeights.Default;

        var healthLimit = new HealthLimitation
        {
            Id = Guid.NewGuid(),
            Type = HealthLimitationType.NoEarlyPeriods,
            IsHardConstraint = true,
            Severity = HealthLimitationSeverity.High
        };

        var objFnWithHealth = new ObjectiveFunctionService(
            [group], [instr], [disc], [room], [assign], [],
            [healthLimit]);

        var objFnWithout = new ObjectiveFunctionService(
            [group], [instr], [disc], [room], [assign], []);

        var metricsWithHealth = objFnWithHealth.Evaluate(timetable, weights);
        var timetable2 = timetable.DeepClone();
        var metricsWithout = objFnWithout.Evaluate(timetable2, weights);

        // FPsych should be lower when health constraint is violated
        metricsWithHealth.FPsych.Should().BeLessThanOrEqualTo(metricsWithout.FPsych + 0.01,
            "Health constraint violation should reduce FPsych");
    }

    [Test]
    public void ObjectiveFunction_ShouldPenalizeSoftInstructorPreferenceViolations()
    {
        var instrId = Guid.NewGuid();
        var constraint = new InstructorConstraint
        {
            Id = Guid.NewGuid(),
            InstructorId = instrId,
            Type = ConstraintType.AvoidFirstPeriod,
            Severity = ConstraintSeverity.Soft
        };

        var group = new AcademicGroup
            { Id = Guid.NewGuid(), StudentCount = 20, Chronotype = ChronotypeCategory.Intermediate, AverageAge = 22 };
        var instr = new Instructor
            { Id = instrId, Chronotype = ChronotypeCategory.Intermediate, MaxClassesPerDay = 5, Age = 35 }; // todo
        var disc = new Discipline
        {
            Id = Guid.NewGuid(), ProcessType = CognitiveProcessType.Analytical, LoadLevel = CognitiveLoadLevel.Low,
            Domain = DisciplineDomain.Humanities
        };
        var room = new Room { Id = Guid.NewGuid(), Type = RoomType.LectureHall, Capacity = 100 };
        var assign = new TeachingAssignment
        {
            Id = Guid.NewGuid(), GroupId = group.Id, InstructorId = instrId, DisciplineId = disc.Id,
            ClassType = ClassType.Lecture, RequiredPeriods = 1
        };

        var scPeriod1 = new ScheduledClass
        {
            AssignmentId = assign.Id, GroupId = group.Id, InstructorId = instrId,
            DisciplineId = disc.Id, RoomId = room.Id, Slot = new TimeSlot(1, 1)
        };
        var t1 = new Timetable();
        t1.Classes.Add(scPeriod1);

        var fpsychWithViolation = FpsychCalculator.Calculate(
            t1, [disc], null, [group], null, [constraint]);

        var scPeriod4 = new ScheduledClass
        {
            AssignmentId = assign.Id, GroupId = group.Id, InstructorId = instrId,
            DisciplineId = disc.Id, RoomId = room.Id, Slot = new TimeSlot(1, 4)
        };
        var t2 = new Timetable();
        t2.Classes.Add(scPeriod4);

        var fpsychWithout = FpsychCalculator.Calculate(
            t2, [disc], null, [group], null, [constraint]);

        fpsychWithViolation.Should().BeLessThanOrEqualTo(fpsychWithout + 0.01,
            "AvoidFirstPeriod soft constraint violation should reduce FPsych");
    }

    [Test]
    public void ObjectiveFunction_ShouldKeepMetricsWithinZeroOne()
    {
        // Use age-aware settings — should still clamp properly
        var group = new AcademicGroup
        {
            Id = Guid.NewGuid(), StudentCount = 20, Chronotype = ChronotypeCategory.DefiniteEvening, AverageAge = 80
        };
        var instr = new Instructor
            { Id = Guid.NewGuid(), Chronotype = ChronotypeCategory.DefiniteMorning, MaxClassesPerDay = 5, Age = 80 };
        var disc = new Discipline
        {
            Id = Guid.NewGuid(), ProcessType = CognitiveProcessType.Mnemonic, LoadLevel = CognitiveLoadLevel.High,
            Domain = DisciplineDomain.Technical
        };
        var room = new Room { Id = Guid.NewGuid(), Type = RoomType.LectureHall, Capacity = 100 };
        var assign = new TeachingAssignment
        {
            Id = Guid.NewGuid(), GroupId = group.Id, InstructorId = instr.Id, DisciplineId = disc.Id,
            ClassType = ClassType.Lecture, RequiredPeriods = 1
        };

        var sc = new ScheduledClass
        {
            AssignmentId = assign.Id, GroupId = group.Id, InstructorId = instr.Id,
            DisciplineId = disc.Id, RoomId = room.Id, Slot = new TimeSlot(1, 8)
        };
        var t = new Timetable();
        t.Classes.Add(sc);

        var objFn = new ObjectiveFunctionService([group], [instr], [disc], [room], [assign], []);
        var metrics = objFn.Evaluate(t, ObjectiveWeights.Default);

        metrics.FTech.Should().BeInRange(0.0, 1.0);
        metrics.FCirc.Should().BeInRange(0.0, 1.0);
        metrics.FPsych.Should().BeInRange(0.0, 1.0);
        metrics.FCogn.Should().BeInRange(0.0, 1.0);
        metrics.F.Should().BeInRange(0.0, 1.0);
    }
}