namespace AnthroDispatch.Application.Algorithms.Conformance;

/// <summary>
/// Result of replaying the trace sigma_x of a timetable through the
/// regulatory Petri net N (dissertation §3.4): token counts and the
/// resulting Conform(x) = 0.5*(1-Missing/Consumed) + 0.5*(1-Remaining/Produced).
/// Conform(x) = 1 iff the timetable satisfies every hard constraint
/// (equivalent to 1{C_hard(x)} = 1, §2.2), but unlike that binary indicator
/// this also localises every deviation via <see cref="Violations"/>.
/// </summary>
public sealed record ConformanceResult(
    int Consumed,
    int Produced,
    int Missing,
    int Remaining,
    double Conform,
    List<ConformanceViolation> Violations);
