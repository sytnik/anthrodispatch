using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.Entities.Anthropocentric;
using AnthroDispatch.Domain.Enums;

namespace AnthroDispatch.Application.Algorithms.Objective;

public static class FtechCalculator
{
    public static double Calculate(
        Timetable timetable,
        List<Room> rooms,
        List<Instructor> instructors,
        List<TeachingAssignment> assignments,
        List<AcademicGroup>? groups = null,
        List<InstructorConstraint>? instructorConstraints = null,
        List<HealthLimitation>? healthLimitations = null)
    {
        var conflicts = CountConflicts(timetable, rooms, instructors, assignments, groups, instructorConstraints,
            healthLimitations);
        var confMax = Math.Max(timetable.Classes.Count, 1);
        return Math.Clamp(1.0 - (double)conflicts / confMax, 0.0, 1.0);
    }

    public static int CountConflicts(
        Timetable timetable,
        List<Room> rooms,
        List<Instructor> instructors,
        List<TeachingAssignment> assignments,
        List<AcademicGroup>? groups = null,
        List<InstructorConstraint>? instructorConstraints = null,
        List<HealthLimitation>? healthLimitations = null)
    {
        var conflicts = 0;
        var classes = timetable.Classes;
        var roomDict = rooms.ToDictionary(r => r.Id);
        var instructorDict = instructors.ToDictionary(i => i.Id);
        var assignmentDict = assignments.ToDictionary(a => a.Id);
        var groupDict = groups?.ToDictionary(g => g.Id) ?? new Dictionary<Guid, AcademicGroup>();

        // 1. Instructor double-booking (supports both legacy GroupId and new InstructorIds list)
        conflicts += classes.GroupBy(c => (c.InstructorId, c.Slot)).Count(g => g.Count() > 1);

        // InstructorSetDoubleBooking — any instructor in the list used twice in same slot
        var multiInstructorClasses = classes.Where(c => c.InstructorIds.Count > 0).ToList();
        if (multiInstructorClasses.Count > 0)
        {
            var slotInstructorPairs = multiInstructorClasses
                .SelectMany(c => c.InstructorIds.Select(iid => (iid, c.Slot, classId: c.Id)));
            conflicts += slotInstructorPairs
                .GroupBy(x => (x.iid, x.Slot)).Count(g => g.Count() > 1);
        }

        // 2. Group double-booking
        conflicts += classes.GroupBy(c => (c.GroupId, c.Slot)).Count(g => g.Count() > 1);

        // GroupSetDoubleBooking — any group in the list has another class in same slot
        var multiGroupClasses = classes.Where(c => c.GroupIds.Count > 0).ToList();
        if (multiGroupClasses.Count > 0)
        {
            var slotGroupPairs = multiGroupClasses
                .SelectMany(c => c.GroupIds.Select(gid => (gid, c.Slot, classId: c.Id)));
            conflicts += slotGroupPairs
                .GroupBy(x => (x.gid, x.Slot)).Count(g => g.Count() > 1);
        }

        // 3. Room double-booking
        conflicts += classes.GroupBy(c => (c.RoomId, c.Slot)).Count(g => g.Count() > 1);

        foreach (var sc in classes)
        {
            if (!roomDict.TryGetValue(sc.RoomId, out var room)) continue;

            // 4. Room capacity violation — legacy single-group
            if (groupDict.TryGetValue(sc.GroupId, out var group) && group.StudentCount > room.Capacity)
                conflicts++;

            // RoomCapacityGroupSetViolation — offline/blended multi-group
            if (!sc.IsOnline() && sc.GroupIds.Count > 0)
            {
                var totalStudents = sc.GroupIds.Sum(gid =>
                    groupDict.TryGetValue(gid, out var g) ? g.StudentCount : 0);
                if (totalStudents > room.Capacity) conflicts++;
            }

            // 5. Room type mismatch — legacy ClassType from assignment
            if (assignmentDict.TryGetValue(sc.AssignmentId, out var assignment))
            {
                if (assignment.ClassType == ClassType.Laboratory &&
                    room.Type != RoomType.Laboratory &&
                    room.Type != RoomType.ComputerLab)
                    conflicts++;
            }

            // RoomTypeLessonTypeMismatch — new LessonType field
            if (sc.LessonType == LessonType.Laboratory &&
                room.Type != RoomType.Laboratory &&
                room.Type != RoomType.ComputerLab &&
                room.Type != RoomType.Online)
                conflicts++;
        }

        // 6. Too many classes per instructor per day (> MaxClassesPerDay)
        foreach (var g in classes.GroupBy(c => (c.InstructorId, c.Slot.Day)))
        {
            if (instructorDict.TryGetValue(g.Key.InstructorId, out var instr) &&
                g.Count() > instr.MaxClassesPerDay)
                conflicts++;
        }

        // 7. Missing required assignment periods
        var scheduledPerAssignment = classes.GroupBy(c => c.AssignmentId)
            .ToDictionary(g => g.Key, g => g.Count());
        foreach (var assignment in assignments)
        {
            var scheduled = scheduledPerAssignment.GetValueOrDefault(assignment.Id, 0);
            if (scheduled < assignment.RequiredPeriods)
                conflicts += assignment.RequiredPeriods - scheduled;
        }

        // InstructorHardConstraintViolation
        if (instructorConstraints != null)
        {
            var hardConstraints = instructorConstraints.Where(c => c.Severity == ConstraintSeverity.Hard);
            foreach (var constraint in hardConstraints)
            {
                var violated = constraint.Type switch
                {
                    ConstraintType.AvoidFirstPeriod => classes.Any(sc =>
                        sc.InstructorId == constraint.InstructorId && sc.Slot.Period == 1),
                    ConstraintType.UnavailableDay => constraint.Day.HasValue && classes.Any(sc =>
                        sc.InstructorId == constraint.InstructorId && sc.Slot.Day == constraint.Day),
                    ConstraintType.UnavailablePeriod => constraint is { Day: not null, Period: not null } &&
                                                        classes.Any(sc =>
                                                            sc.InstructorId == constraint.InstructorId &&
                                                            sc.Slot.Day == constraint.Day &&
                                                            sc.Slot.Period == constraint.Period),
                    ConstraintType.MaxConsecutiveClasses => constraint.Period.HasValue &&
                                                            HasConsecutiveViolation(classes, constraint.InstructorId,
                                                                constraint.Period.Value),
                    ConstraintType.AvoidLatePeriods => classes.Any(sc =>
                        sc.InstructorId == constraint.InstructorId && sc.Slot.Period >= 7),
                    ConstraintType.RoomOrBuildingRestriction => classes.Any(sc =>
                        sc.InstructorId == constraint.InstructorId &&
                        (constraint.RoomId.HasValue
                            ? sc.RoomId != constraint.RoomId.Value
                            : !string.IsNullOrEmpty(constraint.BuildingCode) &&
                              roomDict.TryGetValue(sc.RoomId, out var r) &&
                              !r.Code.StartsWith(constraint.BuildingCode))),
                    ConstraintType.OnlineOnly => classes.Any(sc =>
                        sc.InstructorId == constraint.InstructorId && !sc.IsOnline()),
                    _ => false
                };
                if (violated) conflicts++;
            }
        }

        // HealthHardConstraintViolation
        if (healthLimitations != null)
        {
            foreach (var limit in healthLimitations.Where(l => l.IsHardConstraint))
            {
                var violated = limit.Type switch
                {
                    HealthLimitationType.NoEarlyPeriods => classes.Any(sc => sc.Slot.Period <= 1),
                    HealthLimitationType.NoLatePeriods => classes.Any(sc => sc.Slot.Period >= 7),
                    _ => false
                };
                if (violated) conflicts++;
            }
        }

        return conflicts;
    }

    private static bool HasConsecutiveViolation(List<ScheduledClass> classes, Guid instructorId, int maxConsecutive)
    {
        foreach (var dayGroup in classes.Where(c => c.InstructorId == instructorId).GroupBy(c => c.Slot.Day))
        {
            var periods = dayGroup.Select(c => c.Slot.Period).OrderBy(p => p).ToList();
            var consecutive = 1;
            for (var i = 1; i < periods.Count; i++)
            {
                if (periods[i] == periods[i - 1] + 1) consecutive++;
                else consecutive = 1;
                if (consecutive > maxConsecutive) return true;
            }
        }

        return false;
    }
}