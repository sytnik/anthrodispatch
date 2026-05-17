using AnthroDispatch.Application.Algorithms.Objective;
using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.Enums;
using AnthroDispatch.Domain.Metrics;
using AnthroDispatch.Domain.ValueObjects;
using FluentAssertions;

namespace AnthroDispatch.Tests.Algorithms;

[TestFixture]
public class ObjectiveFunctionTests
{
    private static (List<AcademicGroup> groups, List<Instructor> instructors,
        List<Discipline> disciplines, List<Room> rooms,
        List<TeachingAssignment> assignments, List<CognitiveCompatibility> compat) BuildFixture()
    {
        var groupId = Guid.NewGuid();
        var instrId = Guid.NewGuid();
        var discId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var assignId = Guid.NewGuid();

        var groups = new List<AcademicGroup>
        {
            new() { Id = groupId, Code = "AE-101", Chronotype = ChronotypeCategory.Intermediate, StudentCount = 25 }
        };
        var instructors = new List<Instructor>
        {
            new()
            {
                Id = instrId, FullName = "Test Instructor", Chronotype = ChronotypeCategory.Intermediate,
                MaxClassesPerDay = 4
            }
        };
        var disciplines = new List<Discipline>
        {
            new()
            {
                Id = discId, Code = "D001", Name = "Math", ProcessType = CognitiveProcessType.Analytical,
                LoadLevel = CognitiveLoadLevel.High, Domain = DisciplineDomain.Technical
            }
        };
        var rooms = new List<Room>
        {
            new() { Id = roomId, Code = "R001", Type = RoomType.LectureHall, Capacity = 100 }
        };
        var assignments = new List<TeachingAssignment>
        {
            new()
            {
                Id = assignId, GroupId = groupId, InstructorId = instrId, DisciplineId = discId,
                ClassType = ClassType.Lecture, RequiredPeriods = 1
            }
        };
        var compat = new List<CognitiveCompatibility>();

        return (groups, instructors, disciplines, rooms, assignments, compat);
    }

    [Test]
    public void ObjectiveFunction_ShouldReturnValueBetweenZeroAndOne()
    {
        var (groups, instr, disc, rooms, asgn, compat) = BuildFixture();
        var svc = new ObjectiveFunctionService(groups, instr, disc, rooms, asgn, compat);
        var timetable = new Timetable();
        timetable.Classes.Add(new ScheduledClass
        {
            AssignmentId = asgn[0].Id,
            GroupId = groups[0].Id,
            InstructorId = instr[0].Id,
            DisciplineId = disc[0].Id,
            RoomId = rooms[0].Id,
            Slot = new TimeSlot(1, 4)
        });
        var weights = ObjectiveWeights.Default;
        var metrics = svc.Evaluate(timetable, weights);

        metrics.F.Should().BeInRange(0.0, 1.0);
        metrics.FTech.Should().BeInRange(0.0, 1.0);
        metrics.FCirc.Should().BeInRange(0.0, 1.0);
        metrics.FPsych.Should().BeInRange(0.0, 1.0);
        metrics.FCogn.Should().BeInRange(0.0, 1.0);
    }

    [Test]
    public void Ftech_ShouldBeClampedToZeroOne_WhenNoClasses()
    {
        var (groups, instr, disc, rooms, asgn, compat) = BuildFixture();
        var timetable = new Timetable();
        var ftech = FtechCalculator.Calculate(timetable, rooms, instr, asgn);
        ftech.Should().BeInRange(0.0, 1.0);
    }

    [Test]
    public void Fcogn_ShouldReturnNeutralWhenNoPairs()
    {
        var compat = new List<CognitiveCompatibility>();
        var timetable = new Timetable();
        var fcogn = FcognCalculator.Calculate(timetable, compat);
        fcogn.Should().BeApproximately(0.5, 0.001);
    }

    [Test]
    public void Fpsych_ShouldBeClampedToZeroOne()
    {
        var (_, _, disc, _, _, _) = BuildFixture();
        var timetable = new Timetable();
        var fpsych = FpsychCalculator.Calculate(timetable, disc);
        fpsych.Should().BeInRange(0.0, 1.0);
    }
}