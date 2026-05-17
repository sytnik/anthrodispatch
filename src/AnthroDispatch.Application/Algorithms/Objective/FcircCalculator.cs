using AnthroDispatch.Domain.Entities;

namespace AnthroDispatch.Application.Algorithms.Objective;

public static class FcircCalculator
{
    private const double Alpha = 0.6;

    public static double Calculate(
        Timetable timetable,
        List<AcademicGroup> groups,
        List<Instructor> instructors)
    {
        if (timetable.Classes.Count == 0) return 0.5;

        var groupDict = groups.ToDictionary(g => g.Id);
        var instructorDict = instructors.ToDictionary(i => i.Id);

        double sum = 0;
        var count = 0;

        foreach (var sc in timetable.Classes)
        {
            if (!groupDict.TryGetValue(sc.GroupId, out var group)) continue;
            if (!instructorDict.TryGetValue(sc.InstructorId, out var instructor)) continue;

            // age-aware calculation
            var gActivity = CircadianActivityCalculator.Calculate(group.Chronotype, sc.Slot.Period, group.AverageAge);
            var iActivity =
                CircadianActivityCalculator.Calculate(instructor.Chronotype, sc.Slot.Period, instructor.Age);
            sum += Alpha * gActivity + (1 - Alpha) * iActivity;
            count++;
        }

        return count == 0 ? 0.5 : Math.Clamp(sum / count, 0.0, 1.0);
    }

    /// <summary>Compute daily Fcirc for a specific day (used by CPC).</summary>
    public static double CalculateForDay(
        Timetable timetable,
        int day,
        List<AcademicGroup> groups,
        List<Instructor> instructors)
    {
        var groupDict = groups.ToDictionary(g => g.Id);
        var instructorDict = instructors.ToDictionary(i => i.Id);

        var dayClasses = timetable.Classes.Where(c => c.Slot.Day == day).ToList();
        if (dayClasses.Count == 0) return 0.5;

        double sum = 0;
        var count = 0;
        foreach (var sc in dayClasses)
        {
            if (!groupDict.TryGetValue(sc.GroupId, out var group)) continue;
            if (!instructorDict.TryGetValue(sc.InstructorId, out var instructor)) continue;
            var gA = CircadianActivityCalculator.Calculate(group.Chronotype, sc.Slot.Period, group.AverageAge);
            var iA = CircadianActivityCalculator.Calculate(instructor.Chronotype, sc.Slot.Period, instructor.Age);
            sum += Alpha * gA + (1 - Alpha) * iA;
            count++;
        }

        return count == 0 ? 0.5 : Math.Clamp(sum / count, 0.0, 1.0);
    }
}