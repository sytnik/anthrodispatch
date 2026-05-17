using AnthroDispatch.Domain.Entities;

namespace AnthroDispatch.Application.Algorithms.Objective;

public static class FcognCalculator
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
                    sum += (s + 1.0) / 2.0;
                    count++;
                }
            }
        }

        return count == 0 ? 0.5 : Math.Clamp(sum / count, 0.0, 1.0);
    }
}