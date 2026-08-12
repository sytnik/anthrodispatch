using AnthroDispatch.Domain.Entities;

namespace AnthroDispatch.Application.Algorithms.ScoreIa;

/// <summary>
/// One member of the ranked X_cand list: a candidate timetable together with
/// its z(x) vector and Score_IA(x) (dissertation §2.4). Ranking is advisory,
/// not directive — the dispatcher retains the right to pick a different
/// candidate or reject the whole set.
/// </summary>
public sealed record RankedCandidate(
    Timetable Timetable,
    CandidateVector Z,
    double ScoreIa);
