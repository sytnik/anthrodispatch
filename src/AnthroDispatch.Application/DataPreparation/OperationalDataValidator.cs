using AnthroDispatch.Application.Abstractions;
using AnthroDispatch.Domain.Entities.Dispatch;

namespace AnthroDispatch.Application.DataPreparation;

/// <summary>Validates an AnthroDispatchDataset before converting it to a DispatchProblem.</summary>
public sealed class OperationalDataValidator
{
    public List<string> Validate(AnthroDispatchDataset dataset)
    {
        var errors = new List<string>();

        // Plans must be ready for scheduling
        foreach (var plan in dataset.CurriculumPlans)
        {
            if (!plan.ReadyForScheduling)
                errors.Add($"CurriculumPlan '{plan.Name}' (Id={plan.Id}) is not marked ReadyForScheduling.");
        }

        // Each calendar referenced by a plan must have terms
        var calendarIds = dataset.CurriculumPlans.Select(p => p.CalendarId).Distinct().ToHashSet();
        foreach (var calId in calendarIds)
        {
            var hasTerm = dataset.AcademicCalendarTerms.Any(t => t.CalendarId == calId);
            if (!hasTerm)
                errors.Add($"AcademicCalendar Id={calId} has no calendar terms.");
        }

        // Schedulability: must have at least one group, one instructor, one room
        if (dataset.Groups.Count == 0) errors.Add("Dataset has no groups.");
        if (dataset.Instructors.Count == 0) errors.Add("Dataset has no instructors.");
        if (dataset.Rooms.Count == 0) errors.Add("Dataset has no rooms.");

        return errors;
    }

    /// <summary>Checks offline room-capacity feasibility.</summary>
    public List<string> ValidateRoomCapacity(AnthroDispatchDataset dataset, IReadOnlyList<AtomicSchedulingUnit> units)
    {
        var warnings = new List<string>();
        var groupDict = dataset.Groups.ToDictionary(g => g.Id);
        var maxCapacity = dataset.Rooms.Count > 0 ? dataset.Rooms.Max(r => r.Capacity) : 0;

        foreach (var unit in units)
        {
            if (unit.IsOnline) continue;
            var total = unit.GroupIds.Sum(gid =>
                groupDict.TryGetValue(gid, out var g) ? g.StudentCount : 0);
            if (total > maxCapacity)
                warnings.Add($"AtomicUnit {unit.Id}: total students {total} exceeds max room capacity {maxCapacity}.");
        }

        return warnings;
    }
}