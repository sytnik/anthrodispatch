using AnthroDispatch.Application.Algorithms.Objective;
using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.Enums;
using AnthroDispatch.Domain.ValueObjects;

namespace AnthroDispatch.Application.Algorithms.Repair;

public sealed class RepairService(
    List<Room> rooms,
    List<Instructor> instructors,
    List<AcademicGroup>? groups = null,
    List<TeachingAssignment>? assignments = null)
{
    private readonly List<AcademicGroup> _groups = groups ?? [];
    private readonly List<TeachingAssignment> _assignments = assignments ?? [];

    public RepairResult Repair(Timetable timetable)
    {
        var classes = timetable.Classes;
        var fixedEntitiesCount = 0;

        // Repair order
        fixedEntitiesCount += FixInstructorDoubleBooking(classes); // 1
        fixedEntitiesCount += FixGroupDoubleBooking(classes); // 2
        fixedEntitiesCount += FixRoomDoubleBooking(classes); // 3
        fixedEntitiesCount += FixRoomCapacity(classes); // 4
        fixedEntitiesCount += FixRoomTypeMismatch(classes); // 5
        fixedEntitiesCount += FixExcessiveDailyWorkload(classes); // 6
        fixedEntitiesCount += FixMissingAssignmentPeriods(classes); // 7

        var remaining = CountRemainingConflicts(classes);
        return new RepairResult(timetable, fixedEntitiesCount, remaining);
    }

    // Крок 7 (2026-08-12, ported from Shared/Logic/AnthroDispatch after a
    // real bug found running AMD against University data: DayWiseCpcCrossover
    // recombines whole days from one parent independently per day — if all
    // of an assignment's periods land on days "lost" to the softmax source
    // choice in both parents, the assignment silently disappears from the
    // child. FtechCalculator's category-7 check only detected this
    // (penalized it in fitness); nothing ever restored it. This step is a
    // best-effort insertion: for each assignment still short of
    // RequiredPeriods, find the first free (room, day, period) with no
    // instructor/group/room conflict and add a class there. Does not trim
    // over-scheduled assignments — that doesn't violate a hard constraint,
    // just wastes GA budget; separate concern.
    private int FixMissingAssignmentPeriods(List<ScheduledClass> classes)
    {
        if (_assignments.Count is 0 || rooms.Count is 0) return 0;

        var fixedCount = 0;
        var scheduledPerAssignment = classes.GroupBy(c => c.AssignmentId)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var assignment in _assignments)
        {
            var scheduled = scheduledPerAssignment.GetValueOrDefault(assignment.Id, 0);
            var deficit = assignment.RequiredPeriods - scheduled;
            for (var i = 0; i < deficit; i++)
            {
                if (!TryPlaceNewClass(classes, assignment)) break;
                fixedCount++;
                scheduledPerAssignment[assignment.Id] = scheduledPerAssignment.GetValueOrDefault(assignment.Id, 0) + 1;
            }
        }

        return fixedCount;
    }

    private bool TryPlaceNewClass(List<ScheduledClass> classes, TeachingAssignment assignment)
    {
        foreach (var room in rooms)
        for (var day = 1; day <= 6; day++)
        for (var period = 1; period <= 8; period++)
        {
            var candidate = new TimeSlot(day, period);
            if (classes.Any(c => c.InstructorId == assignment.InstructorId && c.Slot == candidate)) continue;
            if (classes.Any(c => c.GroupId == assignment.GroupId && c.Slot == candidate)) continue;
            if (classes.Any(c => c.RoomId == room.Id && c.Slot == candidate)) continue;

            classes.Add(new ScheduledClass
            {
                AssignmentId = assignment.Id,
                GroupId = assignment.GroupId,
                InstructorId = assignment.InstructorId,
                DisciplineId = assignment.DisciplineId,
                RoomId = room.Id,
                Slot = candidate
            });
            return true;
        }

        return false;
    }

    private int FixInstructorDoubleBooking(List<ScheduledClass> classes)
    {
        var fixInstructorDoubleBooking = 0;
        var groups = classes.GroupBy(c => (c.InstructorId, c.Slot)).Where(g => g.Count() > 1).ToList();
        foreach (var g in groups)
        {
            foreach (var sc in g.Skip(1).ToList())
            {
                var newSlot = FindBestFreeSlot(classes, sc, avoidInstructorConflict: true);
                if (newSlot.HasValue)
                {
                    sc.Slot = newSlot.Value;
                    fixInstructorDoubleBooking++;
                }
            }
        }

        return fixInstructorDoubleBooking;
    }

    private int FixGroupDoubleBooking(List<ScheduledClass> classes)
    {
        var fixGroupDoubleBooking = 0;
        var groups = classes.GroupBy(c => (c.GroupId, c.Slot)).Where(g => g.Count() > 1).ToList();
        foreach (var g in groups)
        {
            foreach (var sc in g.Skip(1).ToList())
            {
                var newSlot = FindBestFreeSlot(classes, sc, avoidGroupConflict: true);
                if (newSlot.HasValue)
                {
                    sc.Slot = newSlot.Value;
                    fixGroupDoubleBooking++;
                }
            }
        }

        return fixGroupDoubleBooking;
    }

    private int FixRoomDoubleBooking(List<ScheduledClass> classes)
    {
        var fixRoomDoubleBooking = 0;
        var groups = classes.GroupBy(c => (c.RoomId, c.Slot)).Where(g => g.Count() > 1).ToList();
        foreach (var g in groups)
        {
            foreach (var sc in g.Skip(1).ToList())
            {
                var newSlot = FindBestFreeSlot(classes, sc, avoidRoomConflict: true);
                if (newSlot.HasValue)
                {
                    sc.Slot = newSlot.Value;
                    fixRoomDoubleBooking++;
                }
            }
        }

        return fixRoomDoubleBooking;
    }

    // step 4: fix room capacity violations by trying a larger room
    private int FixRoomCapacity(List<ScheduledClass> classes)
    {
        if (_groups.Count == 0) return 0;
        var fixRoomCapacity = 0;
        var groupDict = _groups.ToDictionary(g => g.Id);
        var roomDict = rooms.ToDictionary(r => r.Id);

        // Pre-compute occupied (roomId, slot) pairs for O(1) lookup
        var occupiedRoomSlots = new HashSet<(Guid RoomId, TimeSlot Slot)>(
            classes.Select(c => (c.RoomId, c.Slot)));

        foreach (var sc in classes)
        {
            if (!groupDict.TryGetValue(sc.GroupId, out var group)) continue;
            if (!roomDict.TryGetValue(sc.RoomId, out var room)) continue;
            if (room.Capacity >= group.StudentCount) continue;

            // Try to find a bigger room that is free at this slot (O(rooms) instead of O(rooms × n))
            var biggerRoom = rooms
                .Where(r => r.Id != room.Id && r.Capacity >= group.StudentCount &&
                            !occupiedRoomSlots.Contains((r.Id, sc.Slot)))
                .OrderBy(r => r.Capacity)
                .FirstOrDefault();

            if (biggerRoom != null)
            {
                occupiedRoomSlots.Remove((sc.RoomId, sc.Slot));
                sc.RoomId = biggerRoom.Id;
                occupiedRoomSlots.Add((sc.RoomId, sc.Slot));
                fixRoomCapacity++;
            }
        }

        return fixRoomCapacity;
    }

    // step 5: fix room type mismatch (Lab class needs Lab/ComputerLab)
    private int FixRoomTypeMismatch(List<ScheduledClass> classes)
    {
        // Ported 2026-08-12 (was a //todo stub, never implemented, despite
        // FtechCalculator penalizing this category since it was written).
        // Same pattern as FixRoomCapacity — swap to a compatible room, free
        // at the same slot; best-effort, leaves it if none free.
        var fixRoomTypeMismatch = 0;
        var roomDict = rooms.ToDictionary(r => r.Id);
        var assignmentDict = _assignments.ToDictionary(a => a.Id);
        var occupiedRoomSlots = new HashSet<(Guid RoomId, TimeSlot Slot)>(
            classes.Select(c => (c.RoomId, c.Slot)));

        foreach (var sc in classes)
        {
            if (sc.IsOnline()) continue;
            if (!roomDict.TryGetValue(sc.RoomId, out var room)) continue;
            if (room.Type is RoomType.Laboratory or RoomType.ComputerLab or RoomType.Online) continue;

            var requiresLab = sc.LessonType == LessonType.Laboratory ||
                (assignmentDict.TryGetValue(sc.AssignmentId, out var assignment) &&
                 assignment.ClassType == ClassType.Laboratory);
            if (!requiresLab) continue;

            var suitableRoom = rooms
                .Where(r => (r.Type == RoomType.Laboratory || r.Type == RoomType.ComputerLab) &&
                            !occupiedRoomSlots.Contains((r.Id, sc.Slot)))
                .OrderBy(r => r.Capacity)
                .FirstOrDefault();

            if (suitableRoom != null)
            {
                occupiedRoomSlots.Remove((sc.RoomId, sc.Slot));
                sc.RoomId = suitableRoom.Id;
                occupiedRoomSlots.Add((sc.RoomId, sc.Slot));
                fixRoomTypeMismatch++;
            }
        }

        return fixRoomTypeMismatch;
    }

    private int FixExcessiveDailyWorkload(List<ScheduledClass> classes)
    {
        var fixExcessiveDailyWorkload = 0;
        var instructorDict = instructors.ToDictionary(i => i.Id);
        foreach (var g in classes.GroupBy(c => (c.InstructorId, c.Slot.Day)))
        {
            if (!instructorDict.TryGetValue(g.Key.InstructorId, out var instr)) continue;
            foreach (var sc in g.Skip(instr.MaxClassesPerDay).ToList())
            {
                var newSlot = FindBestFreeSlot(classes, sc);
                if (newSlot.HasValue)
                {
                    sc.Slot = newSlot.Value;
                    fixExcessiveDailyWorkload++;
                }
            }
        }

        return fixExcessiveDailyWorkload;
    }

    /// <summary>
    /// Find a free slot, preferring same day first, then adjacent days.
    /// Sorts candidates by circadian quality for the class's group
    /// Uses HashSets for O(1) conflict lookups instead of O(n) Any() scans.
    /// </summary>
    private TimeSlot? FindBestFreeSlot(
        List<ScheduledClass> classes,
        ScheduledClass sc,
        bool avoidInstructorConflict = false,
        bool avoidGroupConflict = false,
        bool avoidRoomConflict = false)
    {
        var groupDict = _groups.ToDictionary(g => g.Id);

        // Build HashSets once for O(1) lookup — avoids O(n) Any() scans per candidate slot
        var instrOccupied = avoidInstructorConflict
            ? new HashSet<TimeSlot>(
                classes.Where(c => c != sc && c.InstructorId == sc.InstructorId).Select(c => c.Slot))
            : null;
        var groupOccupied = avoidGroupConflict
            ? new HashSet<TimeSlot>(classes.Where(c => c != sc && c.GroupId == sc.GroupId).Select(c => c.Slot))
            : null;
        var roomOccupied = avoidRoomConflict
            ? new HashSet<TimeSlot>(classes.Where(c => c != sc && c.RoomId == sc.RoomId).Select(c => c.Slot))
            : null;

        var daysToTry = new[] { sc.Slot.Day }
            .Concat(Enumerable.Range(1, 6).Where(d => d != sc.Slot.Day))
            .ToList();

        var candidates = new List<(TimeSlot slot, double quality)>();

        foreach (var day in daysToTry)
        {
            for (var period = 1; period <= 8; period++)
            {
                var candidate = new TimeSlot(day, period);
                if (instrOccupied?.Contains(candidate) == true) continue;
                if (groupOccupied?.Contains(candidate) == true) continue;
                if (roomOccupied?.Contains(candidate) == true) continue;

                // sort by circadian quality
                var quality = groupDict.TryGetValue(sc.GroupId, out var group)
                    ? CircadianActivityCalculator.Calculate(group.Chronotype, period)
                    : 0.5;

                candidates.Add((candidate, quality));
            }
        }

        // Return slot with highest circadian quality (same-day candidates tried first via stable sort)
        if (candidates.Count == 0) return null;
        return candidates.OrderByDescending(c => c.quality).First().slot;
    }

    private static int CountRemainingConflicts(List<ScheduledClass> classes)
    {
        var c = 0;
        c += classes.GroupBy(x => (x.InstructorId, x.Slot)).Count(g => g.Count() > 1);
        c += classes.GroupBy(x => (x.GroupId, x.Slot)).Count(g => g.Count() > 1);
        c += classes.GroupBy(x => (x.RoomId, x.Slot)).Count(g => g.Count() > 1);
        return c;
    }
}