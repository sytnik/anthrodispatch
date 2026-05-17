using AnthroDispatch.Application.Algorithms.Objective;
using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.Entities.Anthropocentric;
using AnthroDispatch.Domain.Enums;
using AnthroDispatch.Domain.ValueObjects;
using FluentAssertions;

namespace AnthroDispatch.Tests.Algorithms;

[TestFixture]
public class ConflictDetectorTests
{
    private static Timetable BuildTimetable(IEnumerable<ScheduledClass> classes)
    {
        var t = new Timetable();
        t.Classes.AddRange(classes);
        return t;
    }

    [Test]
    public void ConflictDetector_ShouldDetectInstructorSetDoubleBooking()
    {
        var instrId = Guid.NewGuid();
        var slot = new TimeSlot(1, 3);

        // Two classes with GroupIds list that shares the same instructor at same slot
        var sc1 = new ScheduledClass
        {
            InstructorIds = [instrId], GroupIds = [Guid.NewGuid()],
            DisciplineId = Guid.NewGuid(), RoomId = Guid.NewGuid(), Slot = slot
        };
        var sc2 = new ScheduledClass
        {
            InstructorIds = [instrId], GroupIds = [Guid.NewGuid()],
            DisciplineId = Guid.NewGuid(), RoomId = Guid.NewGuid(), Slot = slot
        };

        var t = BuildTimetable([sc1, sc2]);
        var conflicts = FtechCalculator.CountConflicts(t, [], [], []);
        conflicts.Should().BeGreaterThan(0, "Two classes with same instructor+slot should conflict");
    }

    [Test]
    public void ConflictDetector_ShouldDetectGroupSetDoubleBooking()
    {
        var groupId = Guid.NewGuid();
        var slot = new TimeSlot(2, 4);

        var sc1 = new ScheduledClass
        {
            GroupIds = [groupId], InstructorIds = [Guid.NewGuid()],
            DisciplineId = Guid.NewGuid(), RoomId = Guid.NewGuid(), Slot = slot
        };
        var sc2 = new ScheduledClass
        {
            GroupIds = [groupId], InstructorIds = [Guid.NewGuid()],
            DisciplineId = Guid.NewGuid(), RoomId = Guid.NewGuid(), Slot = slot
        };

        var t = BuildTimetable([sc1, sc2]);
        var conflicts = FtechCalculator.CountConflicts(t, [], [], []);
        conflicts.Should().BeGreaterThan(0, "Two classes with same group+slot should conflict");
    }

    [Test]
    public void ConflictDetector_ShouldDetectRoomCapacityViolationForOfflineGroupSet()
    {
        var roomId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var room = new Room { Id = roomId, Type = RoomType.LectureHall, Capacity = 10 };
        var group = new AcademicGroup { Id = groupId, StudentCount = 50 };

        // Class is offline (FullTime), GroupIds contains the group
        var sc = new ScheduledClass
        {
            RoomId = roomId,
            GroupIds = [groupId],
            InstructorIds = [],
            EducationForm = EducationForm.FullTime,
            Slot = new TimeSlot(1, 2)
        };

        var t = BuildTimetable([sc]);
        var conflicts = FtechCalculator.CountConflicts(t, [room], [], [], [group]);
        conflicts.Should().BeGreaterThan(0, "Offline class with students > room capacity should conflict");
    }

    [Test]
    public void ConflictDetector_ShouldIgnoreRoomCapacityForOnlineClass()
    {
        var roomId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var room = new Room { Id = roomId, Type = RoomType.Online, Capacity = 5 };
        var group = new AcademicGroup { Id = groupId, StudentCount = 500 };

        // Online class — capacity check should be skipped
        var sc = new ScheduledClass
        {
            RoomId = roomId, GroupIds = [groupId], InstructorIds = [],
            EducationForm = EducationForm.Distance,
            LessonType = LessonType.Online,
            Slot = new TimeSlot(1, 3)
        };

        var t = BuildTimetable([sc]);
        var conflicts = FtechCalculator.CountConflicts(t, [room], [], [], [group]);
        // No room capacity conflict for online
        conflicts.Should().Be(0, "Online class should not trigger room capacity violation");
    }

    [Test]
    public void ConflictDetector_ShouldDetectRequiredPeriodsMismatch()
    {
        var assignment = new TeachingAssignment
        {
            Id = Guid.NewGuid(), RequiredPeriods = 3,
            GroupId = Guid.NewGuid(), InstructorId = Guid.NewGuid(),
            DisciplineId = Guid.NewGuid(), ClassType = ClassType.Lecture
        };
        // Only 1 class scheduled, but 3 required
        var sc = new ScheduledClass
        {
            AssignmentId = assignment.Id, GroupId = assignment.GroupId,
            InstructorId = assignment.InstructorId, DisciplineId = assignment.DisciplineId,
            RoomId = Guid.NewGuid(), Slot = new TimeSlot(1, 1)
        };

        var t = BuildTimetable([sc]);
        var conflicts = FtechCalculator.CountConflicts(t, [], [], [assignment]);
        conflicts.Should().BeGreaterThanOrEqualTo(2, "Missing 2 of 3 required periods → 2 conflicts");
    }

    [Test]
    public void ConflictDetector_ShouldDetectAvoidFirstPeriodViolation()
    {
        var instrId = Guid.NewGuid();
        var constraint = new InstructorConstraint
        {
            Id = Guid.NewGuid(),
            InstructorId = instrId,
            Type = ConstraintType.AvoidFirstPeriod,
            Severity = ConstraintSeverity.Hard
        };
        var sc = new ScheduledClass
        {
            InstructorId = instrId, GroupId = Guid.NewGuid(),
            DisciplineId = Guid.NewGuid(), RoomId = Guid.NewGuid(),
            Slot = new TimeSlot(1, 1) // period 1, violates constraint
        };

        var t = BuildTimetable([sc]);
        var conflicts = FtechCalculator.CountConflicts(t, [], [], [], null, [constraint]);
        conflicts.Should().BeGreaterThan(0, "AvoidFirstPeriod hard constraint at period 1 should conflict");
    }

    [Test]
    public void ConflictDetector_ShouldDetectUnavailableDayViolation()
    {
        var instrId = Guid.NewGuid();
        var constraint = new InstructorConstraint
        {
            Id = Guid.NewGuid(),
            InstructorId = instrId,
            Type = ConstraintType.UnavailableDay,
            Severity = ConstraintSeverity.Hard,
            Day = 3
        };
        var sc = new ScheduledClass
        {
            InstructorId = instrId, GroupId = Guid.NewGuid(),
            DisciplineId = Guid.NewGuid(), RoomId = Guid.NewGuid(),
            Slot = new TimeSlot(3, 2) // day 3, violates constraint
        };

        var t = BuildTimetable([sc]);
        var conflicts = FtechCalculator.CountConflicts(t, [], [], [], null, [constraint]);
        conflicts.Should().BeGreaterThan(0, "UnavailableDay hard constraint should detect violation");
    }

    [Test]
    public void ConflictDetector_ShouldDetectMaxConsecutiveClassesViolation()
    {
        var instrId = Guid.NewGuid();
        var instr = new Instructor { Id = instrId, MaxClassesPerDay = 5, MaxConsecutiveClasses = 2 };
        var constraint = new InstructorConstraint
        {
            Id = Guid.NewGuid(),
            InstructorId = instrId,
            Type = ConstraintType.MaxConsecutiveClasses,
            Severity = ConstraintSeverity.Hard,
            Period = instr.MaxConsecutiveClasses // max 2 consecutive
        };

        // 3 consecutive periods: 1, 2, 3 — should violate max 2
        var classes = Enumerable.Range(1, 3).Select(p => new ScheduledClass
        {
            InstructorId = instrId, GroupId = Guid.NewGuid(),
            DisciplineId = Guid.NewGuid(), RoomId = Guid.NewGuid(),
            Slot = new TimeSlot(1, p)
        }).ToList();

        var t = BuildTimetable(classes);
        var conflicts = FtechCalculator.CountConflicts(t, [], [instr], [], null, [constraint]);
        conflicts.Should().BeGreaterThan(0, "3 consecutive classes violates MaxConsecutiveClasses=2");
    }

    [Test]
    public void ConflictDetector_ShouldDetectHealthHardConstraintViolation()
    {
        var limit = new HealthLimitation
        {
            Id = Guid.NewGuid(),
            Type = HealthLimitationType.NoEarlyPeriods,
            Severity = HealthLimitationSeverity.High,
            IsHardConstraint = true
        };
        var sc = new ScheduledClass
        {
            InstructorId = Guid.NewGuid(), GroupId = Guid.NewGuid(),
            DisciplineId = Guid.NewGuid(), RoomId = Guid.NewGuid(),
            Slot = new TimeSlot(1, 1) // period 1 = early period
        };

        var t = BuildTimetable([sc]);
        var conflicts = FtechCalculator.CountConflicts(t, [], [], [], null, null, [limit]);
        conflicts.Should().BeGreaterThan(0, "NoEarlyPeriods hard constraint with class at period 1 should conflict");
    }
}