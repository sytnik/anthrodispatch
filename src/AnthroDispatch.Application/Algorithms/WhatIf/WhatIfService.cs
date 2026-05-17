using AnthroDispatch.Application.Algorithms.Explanation;
using AnthroDispatch.Application.Algorithms.Objective;
using AnthroDispatch.Application.Algorithms.Repair;
using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.Enums;
using AnthroDispatch.Domain.Metrics;
using AnthroDispatch.Domain.ValueObjects;

namespace AnthroDispatch.Application.Algorithms.WhatIf;

public sealed class WhatIfService(ObjectiveFunctionService objFn, RepairService repair)
{
    private const double Eta = 0.7; // weight for F(x,w) in Fdyn
    private readonly RiskModelService _risk = new(); // todo

    // Scenario 1: Instructor unavailable for a day or period
    public WhatIfResult InstructorUnavailable(
        Timetable original, ObjectiveWeights weights,
        Guid instructorId, int day, int? period = null)
    {
        var candidate = original.DeepClone();
        var affected = candidate.Classes
            .Where(c => c.InstructorId == instructorId &&
                        c.Slot.Day == day &&
                        (period == null || c.Slot.Period == period))
            .ToList();

        foreach (var sc in affected)
        {
            var newDay = day < 6 ? day + 1 : day - 1;
            sc.Slot = sc.Slot with { Day = newDay };
        }

        repair.Repair(candidate);
        return BuildResult(original, candidate, weights,
            $"Moved {affected.Count} class(es) of instructor {instructorId} from day {day} to adjacent day.");
    }

    // Scenario 2: Room unavailable
    public WhatIfResult RoomUnavailable(
        Timetable original, ObjectiveWeights weights,
        Guid roomId, int day, List<Room> alternativeRooms)
    {
        var candidate = original.DeepClone();
        var affected = candidate.Classes.Where(c => c.RoomId == roomId && c.Slot.Day == day).ToList();
        var alts = alternativeRooms.Where(r => r.Id != roomId).ToList();
        if (alts.Count > 0)
            foreach (var sc in affected)
                sc.RoomId = alts[0].Id;

        repair.Repair(candidate);
        return BuildResult(original, candidate, weights,
            $"Reassigned {affected.Count} class(es) from room {roomId} on day {day}.");
    }

    // Scenario 3: Group cannot attend a period
    public WhatIfResult GroupUnavailable(
        Timetable original, ObjectiveWeights weights,
        Guid groupId, int day, int period)
    {
        var candidate = original.DeepClone();
        var affected = candidate.Classes
            .Where(c => c.GroupId == groupId && c.Slot.Day == day && c.Slot.Period == period)
            .ToList();

        foreach (var sc in affected)
        {
            // Move to next available period in same day
            var newPeriod = period < 8 ? period + 1 : period - 1;
            sc.Slot = new TimeSlot(day, newPeriod);
        }

        repair.Repair(candidate);
        return BuildResult(original, candidate, weights,
            $"Moved {affected.Count} class(es) for group {groupId} away from day {day} period {period}.");
    }

    // Scenario 4: Discipline must be moved
    public WhatIfResult DisciplineMoved(
        Timetable original, ObjectiveWeights weights,
        Guid disciplineId, int targetDay, int targetPeriod)
    {
        var candidate = original.DeepClone();
        var affected = candidate.Classes.Where(c => c.DisciplineId == disciplineId).ToList();
        foreach (var sc in affected)
            sc.Slot = new TimeSlot(targetDay, targetPeriod);

        repair.Repair(candidate);
        return BuildResult(original, candidate, weights,
            $"Moved {affected.Count} class(es) of discipline {disciplineId} to day {targetDay} period {targetPeriod}.");
    }

    // Scenario 5: Weight configuration changed
    public WhatIfResult WeightsChanged(
        Timetable original, ObjectiveWeights oldWeights, ObjectiveWeights newWeights)
    {
        var origMetrics = objFn.Evaluate(original, oldWeights);
        var clone = original.DeepClone();
        var newMetrics = objFn.Evaluate(clone, newWeights);

        return BuildResultFromMetrics(original, clone, origMetrics, newMetrics, 0,
            "Weight configuration changed — timetable structure unchanged.",
            $"F changed by {newMetrics.F - origMetrics.F:+0.000;-0.000} due to new weight vector.");
    }

    // ── New Scenarios ─────────────────────────────────────────────────────────

    // Scenario 6: Instructor constraint (e.g. AvoidFirstPeriod)
    public WhatIfResult InstructorConstraintApplied(
        Timetable original, ObjectiveWeights weights,
        Guid instructorId, ConstraintType constraintType, int? day = null, int? period = null)
    {
        var candidate = original.DeepClone();
        var affected = constraintType switch
        {
            ConstraintType.AvoidFirstPeriod =>
                candidate.Classes.Where(c => c.InstructorId == instructorId && c.Slot.Period == 1).ToList(),
            ConstraintType.UnavailableDay when day.HasValue =>
                candidate.Classes.Where(c => c.InstructorId == instructorId && c.Slot.Day == day).ToList(),
            ConstraintType.AvoidLatePeriods =>
                candidate.Classes.Where(c => c.InstructorId == instructorId && c.Slot.Period >= 7).ToList(),
            _ => []
        };

        foreach (var sc in affected)
        {
            var newPeriod = constraintType == ConstraintType.AvoidFirstPeriod ? 2 :
                constraintType == ConstraintType.AvoidLatePeriods ? 5 : sc.Slot.Period;
            var newDay = constraintType == ConstraintType.UnavailableDay && day.HasValue
                ? day < 6 ? day.Value + 1 : day.Value - 1
                : sc.Slot.Day;
            sc.Slot = new TimeSlot(newDay, newPeriod);
        }

        repair.Repair(candidate);
        return BuildResult(original, candidate, weights,
            $"Applied {constraintType} constraint on instructor {instructorId}: moved {affected.Count} class(es).");
    }

    // Scenario 7: New health limitation appears
    public WhatIfResult HealthLimitationApplied(
        Timetable original, ObjectiveWeights weights,
        HealthLimitationType limitationType)
    {
        var candidate = original.DeepClone();
        var affected = limitationType switch
        {
            HealthLimitationType.NoEarlyPeriods =>
                candidate.Classes.Where(c => c.Slot.Period <= 1).ToList(),
            HealthLimitationType.NoLatePeriods =>
                candidate.Classes.Where(c => c.Slot.Period >= 7).ToList(),
            _ => []
        };

        foreach (var sc in affected)
        {
            var safePeriod = limitationType == HealthLimitationType.NoEarlyPeriods ? 2 : 5;
            sc.Slot = new TimeSlot(sc.Slot.Day, safePeriod);
        }

        repair.Repair(candidate);
        return BuildResult(original, candidate, weights,
            $"Health limitation {limitationType} applied: moved {affected.Count} class(es) to safe periods.");
    }

    // Scenario 8: Room too small for merged lecture (upgrade room)
    public WhatIfResult RoomCapacityInsufficient(
        Timetable original, ObjectiveWeights weights,
        Guid roomId, int requiredCapacity, List<Room> availableRooms)
    {
        var candidate = original.DeepClone();
        var affected = candidate.Classes.Where(c => c.RoomId == roomId).ToList();
        var larger = availableRooms
            .Where(r => r.Id != roomId && r.Capacity >= requiredCapacity)
            .OrderBy(r => r.Capacity)
            .FirstOrDefault();

        if (larger != null)
            foreach (var sc in affected)
                sc.RoomId = larger.Id;

        repair.Repair(candidate);
        var msg = larger != null
            ? $"Reassigned {affected.Count} class(es) from room {roomId} to larger room {larger.Id} (capacity {larger.Capacity})."
            : $"No room with capacity ≥ {requiredCapacity} found; {affected.Count} class(es) remain unresolved.";
        return BuildResult(original, candidate, weights, msg);
    }

    // Scenario 9: Group cannot attend a specific period (group constraint)
    public WhatIfResult GroupConstraintApplied(
        Timetable original, ObjectiveWeights weights,
        Guid groupId, ConstraintType constraintType, int? day = null, int? period = null)
    {
        var candidate = original.DeepClone();
        var affected = constraintType switch
        {
            ConstraintType.AvoidFirstPeriod =>
                candidate.Classes.Where(c => c.GroupId == groupId && c.Slot.Period == 1).ToList(),
            ConstraintType.UnavailablePeriod when day.HasValue && period.HasValue =>
                candidate.Classes.Where(c => c.GroupId == groupId && c.Slot.Day == day && c.Slot.Period == period)
                    .ToList(),
            _ => []
        };

        foreach (var sc in affected)
        {
            var newPeriod = period.HasValue && period.Value < 8 ? period.Value + 1 : 2;
            sc.Slot = new TimeSlot(sc.Slot.Day, newPeriod);
        }

        repair.Repair(candidate);
        return BuildResult(original, candidate, weights,
            $"Group constraint {constraintType} on group {groupId}: moved {affected.Count} class(es).");
    }

    // Scenario 10: Class mode changed from online to offline (or vice versa)
    public WhatIfResult ModeChanged(
        Timetable original, ObjectiveWeights weights,
        Guid disciplineId, EducationForm newForm, List<Room> availableRooms)
    {
        var candidate = original.DeepClone();
        var affected = candidate.Classes.Where(c => c.DisciplineId == disciplineId).ToList();

        var goingOffline = newForm != EducationForm.Distance;

        // Since EducationForm is init-only, replace affected classes with new instances
        foreach (var sc in affected)
        {
            var idx = candidate.Classes.IndexOf(sc);
            var newRoomId = sc.RoomId;
            if (goingOffline)
            {
                var physicalRoom = availableRooms
                    .Where(r => r.Type != RoomType.Online &&
                                !candidate.Classes.Any(c => c != sc && c.RoomId == r.Id && c.Slot == sc.Slot))
                    .OrderBy(r => r.Capacity)
                    .FirstOrDefault();
                if (physicalRoom != null) newRoomId = physicalRoom.Id;
            }

            candidate.Classes[idx] = new ScheduledClass
            {
                Id = sc.Id,
                AssignmentId = sc.AssignmentId,
                GroupId = sc.GroupId,
                InstructorId = sc.InstructorId,
                DisciplineId = sc.DisciplineId,
                RoomId = newRoomId,
                Slot = sc.Slot,
                AtomicUnitId = sc.AtomicUnitId,
                SourceLearningAssignmentId = sc.SourceLearningAssignmentId,
                GroupIds = sc.GroupIds,
                InstructorIds = sc.InstructorIds,
                LessonType = sc.LessonType,
                EducationForm = newForm // new form applied here
            };
        }

        repair.Repair(candidate);
        return BuildResult(original, candidate, weights,
            $"Mode changed to {newForm} for discipline {disciplineId}: {affected.Count} class(es) updated.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private WhatIfResult BuildResult(
        Timetable original, Timetable candidate, ObjectiveWeights weights, string message)
    {
        var origMetrics = original.Metrics ?? objFn.Evaluate(original, weights);
        var candMetrics = objFn.Evaluate(candidate, weights);
        var changed = CountChanged(original, candidate);

        var fStable = RiskModelService.FStable(candidate, original);
        var fDyn = Eta * candMetrics.F + (1 - Eta) * fStable;

        var riskBefore = RiskModelService.Calculate(origMetrics);
        var riskAfter = RiskModelService.Calculate(candMetrics, fStable);

        return new WhatIfResult(
            Guid.NewGuid(), original, candidate, origMetrics, candMetrics,
            candMetrics.F - origMetrics.F, fDyn, changed,
            [
                message,
                changed == 0 ? "No schedule changes required." : $"{changed} class slot(s) changed after repair.",
                $"Circadian alignment changed by {candMetrics.FCirc - origMetrics.FCirc:+0.000;-0.000}.",
                $"Risk before: {riskBefore:F3}, risk after: {riskAfter:F3}.",
                $"Fdyn = {fDyn:F4}  (Fstable = {fStable:F4})"
            ]);
    }

    private static WhatIfResult BuildResultFromMetrics(
        Timetable original, Timetable candidate,
        TimetableMetrics origMetrics, TimetableMetrics candMetrics,
        int changed, params string[] messages)
    {
        var fStable = RiskModelService.FStable(candidate, original);
        var fDyn = Eta * candMetrics.F + (1 - Eta) * fStable;
        return new WhatIfResult(
            Guid.NewGuid(), original, candidate, origMetrics, candMetrics,
            candMetrics.F - origMetrics.F, fDyn, changed, messages.ToList());
    }

    private static int CountChanged(Timetable original, Timetable candidate)
        => original.Classes.Zip(candidate.Classes, (o, c) => o.Slot != c.Slot ? 1 : 0).Sum();
}