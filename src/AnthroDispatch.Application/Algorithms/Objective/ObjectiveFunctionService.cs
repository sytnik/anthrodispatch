using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.Entities.Anthropocentric;
using AnthroDispatch.Domain.Metrics;

namespace AnthroDispatch.Application.Algorithms.Objective;

public sealed class ObjectiveFunctionService(
    List<AcademicGroup> groups,
    List<Instructor> instructors,
    List<Discipline> disciplines,
    List<Room> rooms,
    List<TeachingAssignment> assignments,
    List<CognitiveCompatibility> compatibilities,
    List<HealthLimitation>? healthLimitations = null,
    List<InstructorConstraint>? instructorConstraints = null)
{
    public TimetableMetrics Evaluate(Timetable timetable, ObjectiveWeights weights)
    {
        var fTech = FtechCalculator.Calculate(timetable, rooms, instructors, assignments, groups,
            instructorConstraints, healthLimitations);
        var fCirc = FcircCalculator.Calculate(timetable, groups, instructors);
        var fPsych = FpsychCalculator.Calculate(timetable, disciplines, compatibilities, groups, healthLimitations,
            instructorConstraints);
        var fCogn = FcognCalculator.Calculate(timetable, compatibilities);
        var cInterf = CInterfCalculator.Calculate(timetable, compatibilities);
        var f = weights.Tech * fTech + weights.Circ * fCirc + weights.Psych * fPsych + weights.Cogn * fCogn;
        var conflicts = FtechCalculator.CountConflicts(timetable, rooms, instructors, assignments, groups,
            instructorConstraints, healthLimitations);

        var metrics = new TimetableMetrics
        {
            FTech = fTech,
            FCirc = fCirc,
            FPsych = fPsych,
            FCogn = fCogn,
            CInterf = cInterf,
            F = f,
            Conflicts = conflicts
        };
        timetable.Metrics = metrics;
        return metrics;
    }
}