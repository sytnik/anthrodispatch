using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.Metrics;

namespace AnthroDispatch.Application.Algorithms.Explanation;

/// <summary>
/// Risk model: Risk = δ1*Rconflict + δ2*Rstress + δ3*Rcognitive + δ4*Rchange
/// Default δ = (0.30, 0.30, 0.25, 0.15)
/// </summary>
public sealed class RiskModelService
{
    private const double Delta1 = 0.30; // conflict
    private const double Delta2 = 0.30; // stress
    private const double Delta3 = 0.25; // cognitive
    private const double Delta4 = 0.15; // change

    public static double Calculate(TimetableMetrics metrics, double? fStable = null)
    {
        var rConflict = metrics.FTech < 1.0 ? 1.0 - metrics.FTech : 0.0;
        var rStress = 1.0 - metrics.FPsych;
        var rCognitive = metrics.CInterf; // C_interf(x) — negative pairs only (§2.2/§2.4), not 1-FCogn
        var rChange = fStable.HasValue ? 1.0 - fStable.Value : 0.0; // 0 for initial optimization

        return Delta1 * rConflict
               + Delta2 * rStress
               + Delta3 * rCognitive
               + Delta4 * rChange;
    }

    /// <summary>
    /// Stability score: Fstable = 1 - changes/changesMax
    /// </summary>
    public static double FStable(Timetable current, Timetable previous)
    {
        var changesMax = Math.Max(current.Classes.Count, 1);
        var prevSlots = previous.Classes.ToDictionary(c => c.Id, c => c.Slot);
        var changes = current.Classes.Count(c => prevSlots.TryGetValue(c.Id, out var s) && s != c.Slot);
        return Math.Clamp(1.0 - (double)changes / changesMax, 0.0, 1.0);
    }
}