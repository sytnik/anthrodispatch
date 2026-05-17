using AnthroDispatch.Application.Algorithms.Objective;
using AnthroDispatch.Application.Algorithms.Repair;
using AnthroDispatch.Domain.Entities;

namespace AnthroDispatch.Application.Algorithms.Cpc;

public sealed class DayWiseCpcCrossover(
    List<AcademicGroup> groups,
    List<Instructor> instructors,
    RepairService repair,
    double gamma,
    Random rng)
{
    public Timetable Crossover(Timetable parentA, Timetable parentB)
    {
        var child = new Timetable();

        for (var day = 1; day <= 6; day++)
        {
            var cA = FcircCalculator.CalculateForDay(parentA, day, groups, instructors);
            var cB = FcircCalculator.CalculateForDay(parentB, day, groups, instructors);

            var expA = Math.Exp(gamma * cA);
            var expB = Math.Exp(gamma * cB);
            var pA = expA / (expA + expB);

            var source = rng.NextDouble() < pA ? parentA : parentB;
            var day1 = day;
            var dayClasses = source.Classes.Where(c => c.Slot.Day == day1);
            foreach (var sc in dayClasses)
            {
                child.Classes.Add(new ScheduledClass
                {
                    AssignmentId = sc.AssignmentId,
                    GroupId = sc.GroupId,
                    InstructorId = sc.InstructorId,
                    DisciplineId = sc.DisciplineId,
                    RoomId = sc.RoomId,
                    Slot = sc.Slot
                });
            }
        }

        repair.Repair(child);
        return child;
    }
}