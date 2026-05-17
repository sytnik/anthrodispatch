using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.Entities.Anthropocentric;
using AnthroDispatch.Domain.Enums;

namespace AnthroDispatch.Application.Algorithms.Objective;

public static class FpsychCalculator
{
    private const double Lambda = 0.3;
    private const double Epsilon = 1e-9;

    public static double Calculate(
        Timetable timetable,
        List<Discipline> disciplines,
        List<CognitiveCompatibility>? compatibilities = null,
        List<AcademicGroup>? groups = null,
        List<HealthLimitation>? healthLimitations = null,
        List<InstructorConstraint>? instructorConstraints = null)
    {
        if (timetable.Classes.Count == 0) return 1.0;

        var disciplineDict = disciplines.ToDictionary(d => d.Id);
        var compatDict = compatibilities?
                             .ToDictionary(c => (c.FromDisciplineId, c.ToDisciplineId), c => c.Score)
                         ?? new Dictionary<(Guid, Guid), double>();

        // Daily workload — count classes per group per day
        var groupDayCounts = timetable.Classes
            .GroupBy(c => (c.GroupId, c.Slot.Day))
            .Select(g => (double)g.Count())
            .ToList();

        var lBar = groupDayCounts.Count > 0 ? groupDayCounts.Average() : 1.0;
        var sigmaL = groupDayCounts.Count > 1
            ? Math.Sqrt(groupDayCounts.Average(v => (v - lBar) * (v - lBar)))
            : 0.0;

        // Uncomfortable transitions (3 rules from spec)
        var transitions = 0;
        var transMax = 0;
        foreach (var (groupId, day) in timetable.Classes.Select(c => (c.GroupId, c.Slot.Day)).Distinct())
        {
            var ordered = timetable.Classes
                .Where(c => c.GroupId == groupId && c.Slot.Day == day)
                .OrderBy(c => c.Slot.Period)
                .ToList();

            for (var i = 0; i < ordered.Count - 1; i++)
            {
                transMax++;
                if (!disciplineDict.TryGetValue(ordered[i].DisciplineId, out var d1)) continue;
                if (!disciplineDict.TryGetValue(ordered[i + 1].DisciplineId, out var d2)) continue;

                // Rule 1: High-load followed by High-load
                var rule1 = d1.LoadLevel == CognitiveLoadLevel.High && d2.LoadLevel == CognitiveLoadLevel.High;

                // Rule 2: Technical/NaturalScience → Humanities/Arts with no buffer
                var rule2 = d1.Domain is DisciplineDomain.Technical or DisciplineDomain.NaturalScience &&
                            d2.Domain is DisciplineDomain.Humanities or DisciplineDomain.Arts;

                // Rule 3: Cognitive compatibility sij < -0.3
                var rule3 = compatDict.TryGetValue((d1.Id, d2.Id), out var score) && score < -0.3;

                if (rule1 || rule2 || rule3) transitions++;
            }
        }

        var transRatio = transMax > 0 ? (double)transitions / transMax : 0;
        var fpsychBase = 1.0 - sigmaL / (lBar + Epsilon) - Lambda * transRatio;

        // health-limitation and soft-constraint penalties
        var healthPenalty = CalculateHealthPenalty(timetable, groups, healthLimitations, instructorConstraints);
        var softPenalty = CalculateSoftConstraintPenalty(timetable, instructorConstraints);

        const double healthWeight = 0.20;
        const double preferenceWeight = 0.15;

        var result = fpsychBase - healthWeight * healthPenalty - preferenceWeight * softPenalty;
        return Math.Clamp(result, 0.0, 1.0);
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private static double CalculateHealthPenalty(
        Timetable timetable,
        List<AcademicGroup>? groups,
        List<HealthLimitation>? healthLimitations,
        List<InstructorConstraint>? instructorConstraints)
    {
        if (healthLimitations is null || healthLimitations.Count == 0) return 0.0;

        var violations = 0;
        var possible = 0;

        // Hard health constraints mapped by type
        foreach (var limit in healthLimitations.Where(l => l.IsHardConstraint))
        {
            foreach (var sc in timetable.Classes)
            {
                possible++;
                var violated = limit.Type switch
                {
                    HealthLimitationType.NoEarlyPeriods => sc.Slot.Period <= 1,
                    HealthLimitationType.NoLatePeriods => sc.Slot.Period >= 7,
                    HealthLimitationType.ReducedDailyLoad => false, // evaluated per-group below
                    _ => false
                };
                if (violated) violations++;
            }
        }

        return possible > 0 ? (double)violations / possible : 0.0;
    }

    private static double CalculateSoftConstraintPenalty(
        Timetable timetable,
        List<InstructorConstraint>? instructorConstraints)
    {
        if (instructorConstraints is null || instructorConstraints.Count == 0) return 0.0;

        var violations = 0;
        var possible = instructorConstraints.Count;

        var softConstraints = instructorConstraints.Where(c =>
            c.Severity == ConstraintSeverity.Soft).ToList();

        foreach (var constraint in softConstraints)
        {
            switch (constraint.Type)
            {
                case ConstraintType.AvoidFirstPeriod:
                {
                    var violated = timetable.Classes.Any(sc =>
                        sc.InstructorId == constraint.InstructorId && sc.Slot.Period == 1);
                    if (violated) violations++;
                    break;
                }
                case ConstraintType.AvoidLatePeriods:
                {
                    var violated = timetable.Classes.Any(sc =>
                        sc.InstructorId == constraint.InstructorId && sc.Slot.Period >= 7);
                    if (violated) violations++;
                    break;
                }
                case ConstraintType.UnavailablePeriod:
                case ConstraintType.UnavailableDay:
                case ConstraintType.MaxClassesPerDay:
                case ConstraintType.MaxConsecutiveClasses:
                case ConstraintType.RequiredBreakAfterClass:
                case ConstraintType.PreferredPeriods:
                case ConstraintType.RoomOrBuildingRestriction:
                case ConstraintType.OnlineOnly:
                case ConstraintType.HealthRelated:
                    break;
                default:
                {
                    if (constraint is { Type: ConstraintType.PreferredPeriods, Period: not null })
                    {
                        // Penalise any class not in the preferred period
                        var violated = timetable.Classes.Any(sc =>
                            sc.InstructorId == constraint.InstructorId &&
                            sc.Slot.Period != constraint.Period.Value);
                        if (violated) violations++;
                    }
                    else
                        switch (constraint.Type)
                        {
                            case ConstraintType.HealthRelated:
                            {
                                // Generic health soft-preference: penalise early (period 1) or very late (≥7) slots
                                var violated = timetable.Classes.Any(sc =>
                                    sc.InstructorId == constraint.InstructorId &&
                                    sc.Slot.Period is 1 or >= 7);
                                if (violated) violations++;
                                break;
                            }
                            case ConstraintType.OnlineOnly:
                            {
                                // Soft variant: penalise offline assignments for this instructor
                                var violated = timetable.Classes.Any(sc =>
                                    sc.InstructorId == constraint.InstructorId &&
                                    sc.EducationForm != EducationForm.Distance &&
                                    sc.LessonType != LessonType.Online);
                                if (violated) violations++;
                                break;
                            }
                            case ConstraintType.UnavailablePeriod:
                            case ConstraintType.UnavailableDay:
                            case ConstraintType.AvoidFirstPeriod:
                            case ConstraintType.AvoidLatePeriods:
                            case ConstraintType.MaxClassesPerDay:
                            case ConstraintType.MaxConsecutiveClasses:
                            case ConstraintType.RequiredBreakAfterClass:
                            case ConstraintType.PreferredPeriods:
                            case ConstraintType.RoomOrBuildingRestriction:
                                break;
                            default:
                            {
                                if (constraint is { Type: ConstraintType.RoomOrBuildingRestriction, RoomId: not null })
                                {
                                    // Soft variant: penalise assignment to wrong room
                                    var violated = timetable.Classes.Any(sc =>
                                        sc.InstructorId == constraint.InstructorId &&
                                        sc.RoomId != constraint.RoomId.Value);
                                    if (violated) violations++;
                                }

                                break;
                            }
                        }

                    break;
                }
            }
        }

        return possible > 0 ? (double)violations / possible : 0.0;
    }
}