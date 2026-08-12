namespace AnthroDispatch.Application.Algorithms.ScoreIa;

/// <summary>
/// z(x): the 7-dimensional decision-support vector for a candidate timetable
/// (dissertation §2.4) — z(x) = [F_tech, F_circ, F_psych, F_cogn, F_stable,
/// Risk, Explainability].
/// </summary>
public sealed record CandidateVector(
    double FTech,
    double FCirc,
    double FPsych,
    double FCogn,
    double FStable,
    double Risk,
    double Explainability);
