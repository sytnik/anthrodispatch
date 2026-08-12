using AnthroDispatch.Application.Abstractions;
using AnthroDispatch.Application.Algorithms.Conformance;
using AnthroDispatch.Application.Algorithms.Genetic;
using AnthroDispatch.Application.Algorithms.Objective;
using AnthroDispatch.Application.Algorithms.Repair;
using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.Enums;
using AnthroDispatch.Domain.Metrics;
using AnthroDispatch.Domain.ValueObjects;
using AnthroDispatch.Infrastructure.MockData;
using FluentAssertions;

namespace AnthroDispatch.Tests.Algorithms;

[TestFixture]
public class ConformanceCheckingServiceTests
{
    private static async Task<(MockDatasetGenerator gen, DatasetGenerationResult dataset)> GetDataset()
    {
        var gen = new MockDatasetGenerator();
        var req = new DatasetGenerationRequest(42, 4, 80, 8, 8, 6);
        var dataset = await gen.GenerateAsync(req);
        return (gen, dataset);
    }

    [Test]
    public void CheckConformance_ConflictFreeTimetable_ShouldHaveConformOne()
    {
        // Dissertation §3.4: Conform(x) = 1 <=> 1{C_hard(x)} = 1. A
        // hand-built, deterministically conflict-free timetable (two
        // classes, disjoint slots/resources, room capacity/type both
        // satisfied) must replay with zero missing/remaining tokens.
        var groupId = Guid.NewGuid();
        var instructorId = Guid.NewGuid();
        var disciplineId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();

        var groups = new List<AcademicGroup> { new() { Id = groupId, Code = "G1", StudentCount = 20 } };
        var rooms = new List<Room> { new() { Id = roomId, Code = "R1", Type = RoomType.LectureHall, Capacity = 30 } };
        var assignments = new List<TeachingAssignment>
        {
            new()
            {
                Id = assignmentId, GroupId = groupId, InstructorId = instructorId, DisciplineId = disciplineId,
                ClassType = ClassType.Lecture, RequiredPeriods = 2
            }
        };

        var timetable = new Timetable();
        timetable.Classes.Add(new ScheduledClass
        {
            AssignmentId = assignmentId, GroupId = groupId, InstructorId = instructorId, DisciplineId = disciplineId,
            RoomId = roomId, Slot = new TimeSlot(1, 1)
        });
        timetable.Classes.Add(new ScheduledClass
        {
            AssignmentId = assignmentId, GroupId = groupId, InstructorId = instructorId, DisciplineId = disciplineId,
            RoomId = roomId, Slot = new TimeSlot(1, 2) // different period: no exclusivity conflict
        });

        var svc = new ConformanceCheckingService(rooms, groups, assignments);
        var result = svc.CheckConformance(timetable);

        result.Missing.Should().Be(0);
        result.Remaining.Should().Be(0);
        result.Conform.Should().Be(1.0);
        result.Violations.Should().BeEmpty();
    }

    [Test]
    public async Task CheckConformance_GroupDoubleBooking_ShouldReportViolationAndReduceConform()
    {
        var (_, ds) = await GetDataset();
        var objFn = new ObjectiveFunctionService(ds.Groups, ds.Instructors, ds.Disciplines, ds.Rooms, ds.Assignments,
            ds.Compatibilities);
        var repair = new RepairService(ds.Rooms, ds.Instructors);
        var core = new GaCore(ds.Groups, ds.Instructors, ds.Disciplines, ds.Rooms, ds.Assignments, ds.Compatibilities,
            objFn, repair, 42);

        var timetable = core.RandomInitialize();
        timetable.Classes.Should().NotBeEmpty();

        // Force a group double-booking: add a second class for the same
        // group in the same slot as an existing one.
        var first = timetable.Classes[0];
        timetable.Classes.Add(new ScheduledClass
        {
            AssignmentId = first.AssignmentId,
            GroupId = first.GroupId,
            InstructorId = Guid.NewGuid(),
            DisciplineId = first.DisciplineId,
            RoomId = Guid.NewGuid(),
            Slot = first.Slot
        });

        var svc = new ConformanceCheckingService(ds.Rooms, ds.Groups, ds.Assignments);
        var result = svc.CheckConformance(timetable);

        result.Missing.Should().BeGreaterThan(0);
        result.Conform.Should().BeLessThan(1.0);
        result.Violations.Should().Contain(v => v.ConstraintType == "GroupDoubleBooking");
    }

    [Test]
    public void CheckConformance_EmptyTimetable_ShouldReportConformOne()
    {
        var svc = new ConformanceCheckingService([], [], []);
        var result = svc.CheckConformance(new Timetable());

        result.Consumed.Should().Be(0);
        result.Conform.Should().Be(1.0, "an empty trace trivially satisfies every hard constraint");
        result.Violations.Should().BeEmpty();
    }

    [Test]
    public void CheckConformance_RoomCapacityExceeded_ShouldReportViolation()
    {
        var groupId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var instructorId = Guid.NewGuid();
        var disciplineId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();

        var groups = new List<AcademicGroup> { new() { Id = groupId, Code = "G1", StudentCount = 50 } };
        var rooms = new List<Room> { new() { Id = roomId, Code = "R1", Type = RoomType.LectureHall, Capacity = 20 } };
        var assignments = new List<TeachingAssignment>
        {
            new()
            {
                Id = assignmentId, GroupId = groupId, InstructorId = instructorId, DisciplineId = disciplineId,
                ClassType = ClassType.Lecture, RequiredPeriods = 1
            }
        };

        var timetable = new Timetable();
        timetable.Classes.Add(new ScheduledClass
        {
            AssignmentId = assignmentId, GroupId = groupId, InstructorId = instructorId, DisciplineId = disciplineId,
            RoomId = roomId, Slot = new TimeSlot(1, 1)
        });

        var svc = new ConformanceCheckingService(rooms, groups, assignments);
        var result = svc.CheckConformance(timetable);

        result.Violations.Should().ContainSingle(v => v.ConstraintType == "RoomCapacityExceeded");
        result.Conform.Should().BeLessThan(1.0);
    }
}
