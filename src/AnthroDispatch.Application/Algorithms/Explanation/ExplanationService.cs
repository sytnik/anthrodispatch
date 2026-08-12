using AnthroDispatch.Application.Algorithms.Objective;
using AnthroDispatch.Domain.Entities;

namespace AnthroDispatch.Application.Algorithms.Explanation;

public sealed class ExplanationService(
    List<AcademicGroup> groups,
    List<Instructor> instructors,
    List<Discipline> disciplines,
    List<CognitiveCompatibility>? compatibilities = null)
{
    private readonly List<CognitiveCompatibility> _compatibilities = compatibilities ?? [];

    public ClassExplanation ExplainClass(Timetable timetable, Guid scheduledClassId)
    {
        var sc = timetable.Classes.FirstOrDefault(c => c.Id == scheduledClassId);
        if (sc is null) return new ClassExplanation(scheduledClassId, [], new(), []);

        var group = groups.FirstOrDefault(g => g.Id == sc.GroupId);
        var instructor = instructors.FirstOrDefault(i => i.Id == sc.InstructorId);
        var discipline = disciplines.FirstOrDefault(d => d.Id == sc.DisciplineId);

        var reasons = new List<string>();
        var scores = new Dictionary<string, double>();
        var tradeOffs = new List<string>();

        var gActivity = group != null ? CircadianActivityCalculator.Calculate(group.Chronotype, sc.Slot.Period) : 0.5;
        var iActivity = instructor != null
            ? CircadianActivityCalculator.Calculate(instructor.Chronotype, sc.Slot.Period)
            : 0.5;

        reasons.Add(
            $"Class '{discipline?.Name ?? sc.DisciplineId.ToString()}' scheduled at Day {sc.Slot.Day}, Period {sc.Slot.Period}.");
        reasons.Add($"Group chronotype score for this slot: {gActivity:F3}.");
        reasons.Add($"Instructor chronotype score for this slot: {iActivity:F3}.");

        var groupConflict = timetable.Classes.Any(c => c != sc && c.GroupId == sc.GroupId && c.Slot == sc.Slot);
        var instrConflict =
            timetable.Classes.Any(c => c != sc && c.InstructorId == sc.InstructorId && c.Slot == sc.Slot);
        if (!groupConflict && !instrConflict)
            reasons.Add("No hard conflicts detected for this slot.");
        else
        {
            if (groupConflict) reasons.Add("WARNING: Group double-booking detected.");
            if (instrConflict) reasons.Add("WARNING: Instructor double-booking detected.");
        }

        // Check previous discipline cognitive compatibility
        var dayClasses = timetable.Classes
            .Where(c => c.GroupId == sc.GroupId && c.Slot.Day == sc.Slot.Day)
            .OrderBy(c => c.Slot.Period).ToList();
        var pos = dayClasses.IndexOf(sc);
        if (pos > 0)
        {
            var prev = dayClasses[pos - 1];
            var compat = _compatibilities.FirstOrDefault(c =>
                c.FromDisciplineId == prev.DisciplineId && c.ToDisciplineId == sc.DisciplineId);
            if (compat != null)
                reasons.Add($"Previous discipline has cognitive compatibility {compat.Score:+0.00;-0.00;0.00}.");
        }

        scores["FCircGroup"] = gActivity;
        scores["FCircInstructor"] = iActivity;
        scores["FCircBlended"] = 0.6 * gActivity + 0.4 * iActivity;

        // Trade-off: what if moved to period+1?
        if (group != null && sc.Slot.Period < 8)
        {
            var altActivity = CircadianActivityCalculator.Calculate(group.Chronotype, sc.Slot.Period + 1);
            var diff = altActivity - gActivity;
            tradeOffs.Add(
                $"Moving to Period {sc.Slot.Period + 1} would change group circadian alignment by {diff:+0.000;-0.000}.");
        }

        return new ClassExplanation(scheduledClassId, reasons, scores, tradeOffs);
    }

    /// <summary>
    /// Identify strongest/weakest component, highest-risk groups,
    /// worst cognitive sequences, overloaded instructors, and suggestions.
    /// </summary>
    public TimetableExplanation ExplainTimetable(Timetable timetable)
    {
        var metrics = timetable.Metrics;
        var strengths = new List<string>();
        var weaknesses = new List<string>();
        var recommendations = new List<string>();
        var scores = new Dictionary<string, double>();

        if (metrics != null)
        {
            scores["FTech"] = metrics.FTech;
            scores["FCirc"] = metrics.FCirc;
            scores["FPsych"] = metrics.FPsych;
            scores["FCogn"] = metrics.FCogn;
            scores["F"] = metrics.F;

            var components = new[]
            {
                ("FTech", metrics.FTech), ("FCirc", metrics.FCirc), ("FPsych", metrics.FPsych), ("FCogn", metrics.FCogn)
            };
            var best = components.OrderByDescending(c => c.Item2).First();
            var worst = components.OrderBy(c => c.Item2).First();

            // Strengths
            strengths.Add($"Strongest component: {best.Item1} = {best.Item2:F3}.");
            if (metrics.Conflicts == 0) strengths.Add("No scheduling conflicts detected.");

            // Weaknesses
            weaknesses.Add($"Weakest component: {worst.Item1} = {worst.Item2:F3}.");
            if (metrics.Conflicts > 0) weaknesses.Add($"{metrics.Conflicts} hard conflict(s) remain.");
        }

        // Highest-risk groups — groups with most evening chronotype classes in morning slots
        var groupDict = groups.ToDictionary(g => g.Id);
        var groupMismatchScores = timetable.Classes
            .Where(c => groupDict.ContainsKey(c.GroupId))
            .GroupBy(c => c.GroupId)
            .Select(g =>
            {
                var grp = groupDict[g.Key];
                var mismatch = g.Average(c =>
                    1.0 - CircadianActivityCalculator.Calculate(grp.Chronotype, c.Slot.Period));
                return (GroupCode: grp.Code, Mismatch: mismatch);
            })
            .OrderByDescending(x => x.Mismatch)
            .Take(3)
            .ToList();

        if (groupMismatchScores.Count > 0)
        {
            weaknesses.Add("Highest-risk groups (most circadian misalignment): " +
                           string.Join(", ", groupMismatchScores.Select(x => $"{x.GroupCode} ({x.Mismatch:F2})")));
            if (groupMismatchScores[0].Mismatch > 0.4)
                recommendations.Add(
                    $"Group {groupMismatchScores[0].GroupCode} has severe circadian mismatch — consider shifting classes to preferred time periods.");
        }

        // Worst cognitive sequences — group-days with most low-compatibility transitions
        var compatDict = _compatibilities.ToDictionary(c => (c.FromDisciplineId, c.ToDisciplineId), c => c.Score);
        var worstSequences = new List<string>();
        foreach (var (groupId, day) in timetable.Classes.Select(c => (c.GroupId, c.Slot.Day)).Distinct())
        {
            var ordered = timetable.Classes
                .Where(c => c.GroupId == groupId && c.Slot.Day == day)
                .OrderBy(c => c.Slot.Period).ToList();
            for (var i = 0; i < ordered.Count - 1; i++)
            {
                var key = (ordered[i].DisciplineId, ordered[i + 1].DisciplineId);
                if (compatDict.TryGetValue(key, out var s) && s < -0.3)
                {
                    var d1Name = disciplines.FirstOrDefault(d => d.Id == ordered[i].DisciplineId)?.Name ?? "Unknown";
                    var d2Name = disciplines.FirstOrDefault(d => d.Id == ordered[i + 1].DisciplineId)?.Name ??
                                 "Unknown";
                    var grpCode = groupDict.TryGetValue(groupId, out var grp) ? grp.Code : groupId.ToString();
                    worstSequences.Add($"{grpCode} Day{day}: '{d1Name}' → '{d2Name}' (s={s:F2})");
                }
            }
        }

        if (worstSequences.Count > 0)
        {
            weaknesses.Add("Worst cognitive sequences: " + string.Join("; ", worstSequences.Take(3)));
            recommendations.Add("Improve cognitive sequencing by pairing complementary subjects.");
        }

        // Overloaded instructors — instructors exceeding MaxClassesPerDay
        var instrDict = instructors.ToDictionary(i => i.Id);
        var overloaded = timetable.Classes
            .GroupBy(c => (c.InstructorId, c.Slot.Day))
            .Where(g => instrDict.TryGetValue(g.Key.InstructorId, out var instr) && g.Count() > instr.MaxClassesPerDay)
            .Select(g => (Name: instrDict[g.Key.InstructorId].FullName, g.Key.Day, Count: g.Count(),
                Max: instrDict[g.Key.InstructorId].MaxClassesPerDay))
            .OrderByDescending(x => x.Count - x.Max)
            .Take(3)
            .ToList();

        if (overloaded.Count > 0)
        {
            weaknesses.Add("Overloaded instructors: " +
                           string.Join("; ", overloaded.Select(x => $"{x.Name} (Day {x.Day}: {x.Count}/{x.Max})")));
            recommendations.Add("Redistribute instructor workload to avoid excessive daily class loads.");
        }

        // Standard recommendations from metrics
        if (metrics != null)
        {
            if (metrics.FCirc < 0.7)
                recommendations.Add(
                    "Consider rescheduling classes for groups with evening chronotype earlier in the day.");
            if (metrics.FPsych < 0.7)
                recommendations.Add("Reduce consecutive high-load sessions to improve psychological comfort.");
            if (metrics.FCogn < 0.7 && worstSequences.Count == 0)
                recommendations.Add("Improve cognitive sequencing by pairing complementary subjects.");
        }

        return new TimetableExplanation(timetable.Id, strengths, weaknesses, recommendations, scores);
    }

    /// <summary>
    /// Explainability(x) (dissertation §2.4): fraction of scheduled classes
    /// for which at least one non-trivial reason with a positive criterion
    /// contribution can be cited — circadian activity above the midpoint of
    /// [0,1], absence of a hard conflict at the slot, or positive cognitive
    /// compatibility with the immediately preceding class of the same
    /// group/day. The 0.5 "above midpoint" threshold for circadian activity
    /// is an implementation choice (not numerically specified in the text).
    /// </summary>
    public double ComputeExplainability(Timetable timetable)
    {
        if (timetable.Classes.Count == 0) return 0.0;
        var positiveCount = timetable.Classes.Count(sc => HasPositiveContribution(timetable, sc));
        return (double)positiveCount / timetable.Classes.Count;
    }

    private bool HasPositiveContribution(Timetable timetable, ScheduledClass sc)
    {
        var group = groups.FirstOrDefault(g => g.Id == sc.GroupId);
        var instructor = instructors.FirstOrDefault(i => i.Id == sc.InstructorId);

        var gActivity = group != null ? CircadianActivityCalculator.Calculate(group.Chronotype, sc.Slot.Period) : 0.5;
        var iActivity = instructor != null
            ? CircadianActivityCalculator.Calculate(instructor.Chronotype, sc.Slot.Period)
            : 0.5;
        if (0.6 * gActivity + 0.4 * iActivity >= 0.5) return true;

        var groupConflict = timetable.Classes.Any(c => c != sc && c.GroupId == sc.GroupId && c.Slot == sc.Slot);
        var instrConflict =
            timetable.Classes.Any(c => c != sc && c.InstructorId == sc.InstructorId && c.Slot == sc.Slot);
        if (!groupConflict && !instrConflict) return true;

        var dayClasses = timetable.Classes
            .Where(c => c.GroupId == sc.GroupId && c.Slot.Day == sc.Slot.Day)
            .OrderBy(c => c.Slot.Period).ToList();
        var pos = dayClasses.IndexOf(sc);
        if (pos > 0)
        {
            var prev = dayClasses[pos - 1];
            var compat = _compatibilities.FirstOrDefault(c =>
                c.FromDisciplineId == prev.DisciplineId && c.ToDisciplineId == sc.DisciplineId);
            if (compat != null && compat.Score > 0) return true;
        }

        return false;
    }
}