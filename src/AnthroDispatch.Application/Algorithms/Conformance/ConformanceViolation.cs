namespace AnthroDispatch.Application.Algorithms.Conformance;

/// <summary>
/// One alignment-based diagnostic (dissertation §3.4): a specific hard
/// constraint that a specific event of the trace violates, with enough
/// context (day, period, resource) to feed directly into the repair
/// procedure's target-slot list instead of a full conflict re-search.
/// </summary>
public sealed record ConformanceViolation(
    Guid ScheduledClassId,
    int Day,
    int Period,
    Guid GroupId,
    Guid InstructorId,
    Guid RoomId,
    string ConstraintType,
    string Description);
