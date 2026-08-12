using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.Enums;

namespace AnthroDispatch.Application.Algorithms.Conformance;

/// <summary>
/// Process-mining conformance checking (dissertation §3.4, third —
/// independent of human/predicted evaluation — verification method):
/// converts a timetable x into an event trace sigma_x, replays it through a
/// regulatory Petri net N of hard constraints C_hard(x) (§2.2), and reports
/// Conform(x) plus alignment-based diagnostics.
///
/// Scope: N's places model the three resource-exclusivity constraints
/// (group/instructor/room double-booking) as genuine capacity-1 Petri net
/// places, replayed in trace order. Room type and room capacity compliance
/// are checked as two additional per-event guards (same categories
/// FtechCalculator already validates for F_tech, kept consistent rather
/// than reimplemented divergently). Instructor/health hard constraints are
/// intentionally out of scope here — they are already covered by F_tech in
/// the objective function; this service's distinguishing value is the
/// resource-exclusivity Petri net and the alignment diagnostics, not an
/// exhaustive re-derivation of every C_hard(x) category.
///
/// Token accounting (implementation choice, documented since the
/// dissertation does not fix an exact algorithm): for every one of the 5
/// guard checks per event, c (consumed) is incremented once. A satisfied
/// guard consumes its token normally and also counts toward p (produced) —
/// production mirrors consumption on the expected path. A violated guard's
/// token was missing (m++); the replay borrows an artificial token to keep
/// going, and that borrowed token is never legitimately absorbed downstream
/// in this exclusive-resource model, so it is also counted as remaining
/// (r = m). This keeps Conform(x) = 1 exactly when C_hard(x) holds (m = 0)
/// and strictly decreasing as violations accumulate, matching §3.4's
/// stated equivalence Conform(x) = 1 &lt;=&gt; 1{C_hard(x)} = 1.
/// </summary>
public sealed class ConformanceCheckingService(
    List<Room> rooms,
    List<AcademicGroup> groups,
    List<TeachingAssignment> assignments)
{
    private readonly Dictionary<Guid, Room> _roomDict = rooms.ToDictionary(r => r.Id);
    private readonly Dictionary<Guid, AcademicGroup> _groupDict = groups.ToDictionary(g => g.Id);
    private readonly Dictionary<Guid, TeachingAssignment> _assignmentDict = assignments.ToDictionary(a => a.Id);

    public ConformanceResult CheckConformance(Timetable timetable)
    {
        // sigma_x: trace ordered by (day, period); Id as a stable tie-break
        // for events sharing the same slot (order among simultaneous events
        // does not affect which exclusivity checks fire, only which one is
        // recorded as the "first claim" on a contested resource).
        var trace = timetable.Classes
            .OrderBy(c => c.Slot.Day).ThenBy(c => c.Slot.Period).ThenBy(c => c.Id)
            .ToList();

        var net = new PetriNet();
        int consumed = 0, produced = 0, missing = 0;
        var violations = new List<ConformanceViolation>();

        foreach (var sc in trace)
        {
            CheckExclusivity(net, $"group:{sc.GroupId}:{sc.Slot.Day}:{sc.Slot.Period}", sc,
                "GroupDoubleBooking", "Group already occupied at this slot by another class.",
                ref consumed, ref produced, ref missing, violations);

            CheckExclusivity(net, $"instructor:{sc.InstructorId}:{sc.Slot.Day}:{sc.Slot.Period}", sc,
                "InstructorDoubleBooking", "Instructor already teaching at this slot.",
                ref consumed, ref produced, ref missing, violations);

            CheckExclusivity(net, $"room:{sc.RoomId}:{sc.Slot.Day}:{sc.Slot.Period}", sc,
                "RoomDoubleBooking", "Room already booked at this slot by another class.",
                ref consumed, ref produced, ref missing, violations);

            CheckGuard(RoomCapacityOk(sc), sc, "RoomCapacityExceeded",
                "Group size exceeds room capacity.", ref consumed, ref produced, ref missing, violations);

            CheckGuard(RoomTypeOk(sc), sc, "RoomTypeMismatch",
                "Room type does not match the required class type.", ref consumed, ref produced, ref missing,
                violations);
        }

        // r = m: every borrowed (missing) token remains an unabsorbed
        // anomaly in this exclusive-resource model (see class doc).
        var remaining = missing;

        var conform = Conform(consumed, produced, missing, remaining);
        return new ConformanceResult(consumed, produced, missing, remaining, conform, violations);
    }

    private static double Conform(int consumed, int produced, int missing, int remaining)
    {
        var missingTerm = consumed > 0 ? 1.0 - (double)missing / consumed : 1.0;
        var remainingTerm = produced > 0 ? 1.0 - (double)remaining / produced : (missing == 0 ? 1.0 : 0.0);
        return 0.5 * missingTerm + 0.5 * remainingTerm;
    }

    private static void CheckExclusivity(
        PetriNet net, string placeId, ScheduledClass sc, string constraintType, string description,
        ref int consumed, ref int produced, ref int missing, List<ConformanceViolation> violations)
    {
        if (!net.HasPlace(placeId)) net.AddPlace(placeId, 1);
        consumed++;
        if (net.TryConsume(placeId))
        {
            produced++;
        }
        else
        {
            missing++;
            violations.Add(new ConformanceViolation(sc.Id, sc.Slot.Day, sc.Slot.Period, sc.GroupId, sc.InstructorId,
                sc.RoomId, constraintType, description));
        }
    }

    private static void CheckGuard(
        bool satisfied, ScheduledClass sc, string constraintType, string description,
        ref int consumed, ref int produced, ref int missing, List<ConformanceViolation> violations)
    {
        consumed++;
        if (satisfied)
        {
            produced++;
        }
        else
        {
            missing++;
            violations.Add(new ConformanceViolation(sc.Id, sc.Slot.Day, sc.Slot.Period, sc.GroupId, sc.InstructorId,
                sc.RoomId, constraintType, description));
        }
    }

    private bool RoomCapacityOk(ScheduledClass sc)
    {
        if (!_roomDict.TryGetValue(sc.RoomId, out var room)) return true; // unknown room: not this check's concern
        if (!_groupDict.TryGetValue(sc.GroupId, out var group)) return true;
        return group.StudentCount <= room.Capacity;
    }

    private bool RoomTypeOk(ScheduledClass sc)
    {
        if (!_roomDict.TryGetValue(sc.RoomId, out var room)) return true;
        if (!_assignmentDict.TryGetValue(sc.AssignmentId, out var assignment)) return true;
        if (assignment.ClassType != ClassType.Laboratory) return true;
        return room.Type is RoomType.Laboratory or RoomType.ComputerLab;
    }
}
