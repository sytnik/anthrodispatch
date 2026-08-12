namespace AnthroDispatch.Api.Endpoints;

/// <summary>
/// Serialized shape of one X_cand entry (dissertation §2.4): rank position,
/// the candidate's classes, its z(x) vector, and Score_IA(x). Stored as JSON
/// in <c>OptimizationRun.CandidatesJson</c> and returned verbatim by
/// <c>GET /api/optimization/{runId}/candidates</c>.
/// </summary>
internal sealed record RankedCandidateDto(
    int Rank,
    Guid TimetableId,
    List<ScheduledClassDto> Classes,
    double FTech,
    double FCirc,
    double FPsych,
    double FCogn,
    double FStable,
    double Risk,
    double Explainability,
    double ScoreIa);
