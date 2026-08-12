using AnthroDispatch.Application.Algorithms.Explanation;
using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.Metrics;

namespace AnthroDispatch.Application.Algorithms.ScoreIa;

/// <summary>
/// IA ranking layer: builds z(x) for each candidate in X_cand and ranks them
/// by the advisory (not directive) Score_IA(x) = rho1*F(x,w) + rho2*Fstable
/// - rho3*Risk(x) + rho4*Explainability(x), rho = (0.55, 0.20, 0.15, 0.10)
/// (dissertation §2.4).
/// </summary>
public sealed class ScoreIaService(ExplanationService explanationService)
{
    private const double Rho1 = 0.55; // F(x,w)
    private const double Rho2 = 0.20; // Fstable
    private const double Rho3 = 0.15; // Risk
    private const double Rho4 = 0.10; // Explainability

    public CandidateVector BuildZ(Timetable candidate, Timetable? previous)
    {
        var metrics = candidate.Metrics
                      ?? throw new InvalidOperationException("Candidate must be evaluated before scoring.");

        // No prior approved version to compare against (e.g. first-ever
        // dispatch run): treat as fully stable rather than penalise Risk's
        // change component against a non-existent baseline.
        var fStable = previous != null ? RiskModelService.FStable(candidate, previous) : 1.0;
        var risk = RiskModelService.Calculate(metrics, previous != null ? fStable : null);
        var explainability = explanationService.ComputeExplainability(candidate);

        return new CandidateVector(metrics.FTech, metrics.FCirc, metrics.FPsych, metrics.FCogn, fStable, risk,
            explainability);
    }

    public double Score(TimetableMetrics metrics, CandidateVector z)
        => Rho1 * metrics.F + Rho2 * z.FStable - Rho3 * z.Risk + Rho4 * z.Explainability;

    /// <summary>
    /// Ranks X_cand descending by Score_IA. The full ordered list is
    /// returned — the dispatcher chooses, Score_IA only advises.
    /// </summary>
    public List<RankedCandidate> RankCandidates(IEnumerable<Timetable> candidates, Timetable? previous = null)
    {
        return candidates
            .Select(c =>
            {
                var z = BuildZ(c, previous);
                var score = Score(c.Metrics!, z);
                return new RankedCandidate(c, z, score);
            })
            .OrderByDescending(r => r.ScoreIa)
            .ToList();
    }
}
