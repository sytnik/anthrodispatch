using AnthroDispatch.Domain.Entities;

namespace AnthroDispatch.Application.Algorithms.Objective;

/// <summary>
/// C_interf(x) (dissertation §2.2): (1/|pairs(x)|)·Σmax(-s_kl, 0) over
/// adjacent same-day pairs — the negative-interference-only counterpart of
/// FcognCalculator, feeding RiskModelService's Risk_cognitive term (§2.4).
/// </summary>
public static class CInterfCalculator
{
    public static double Calculate(
        Timetable timetable,
        List<CognitiveCompatibility> compatibilities)
    {
        var compatDict = compatibilities.ToDictionary(c => (c.FromDisciplineId, c.ToDisciplineId), c => c.Score);

        double sum = 0;
        var count = 0;

        foreach (var (groupId, day) in timetable.Classes.Select(c => (c.GroupId, c.Slot.Day)).Distinct())
        {
            var ordered = timetable.Classes
                .Where(c => c.GroupId == groupId && c.Slot.Day == day)
                .OrderBy(c => c.Slot.Period)
                .ToList();

            for (var i = 0; i < ordered.Count - 1; i++)
            {
                var key = (ordered[i].DisciplineId, ordered[i + 1].DisciplineId);
                if (compatDict.TryGetValue(key, out var s))
                {
                    sum += Math.Max(-s, 0.0);
                    count++;
                }
            }
        }

        // No scored adjacent pairs means no measured interference, unlike
        // FcognCalculator's neutral 0.5 default — here 0 is the correct "no
        // data" value since C_interf only ever accumulates non-negative terms.
        return count == 0 ? 0.0 : Math.Clamp(sum / count, 0.0, 1.0);
    }
}
