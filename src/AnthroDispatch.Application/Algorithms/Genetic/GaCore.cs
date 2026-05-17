using AnthroDispatch.Application.Algorithms.Objective;
using AnthroDispatch.Application.Algorithms.Repair;
using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.ValueObjects;

namespace AnthroDispatch.Application.Algorithms.Genetic;

/// <summary>Shared initialization, selection, mutation helpers for GA variants.</summary>
public sealed class GaCore(
    List<AcademicGroup> groups,
    List<Instructor> instructors,
    List<Discipline> disciplines,
    List<Room> rooms,
    List<TeachingAssignment> assignments,
    List<CognitiveCompatibility> compatibilities,
    ObjectiveFunctionService objFn,
    RepairService repair,
    int seed)
{
    private readonly List<AcademicGroup> _groups = groups;
    private readonly List<Instructor> _instructors = instructors;
    private readonly List<Discipline> _disciplines = disciplines;
    private readonly List<CognitiveCompatibility> _compatibilities = compatibilities;
    private readonly ObjectiveFunctionService _objFn = objFn;
    public Random Rng { get; } = new(seed);

    public Timetable RandomInitialize()
    {
        var t = new Timetable();
        foreach (var a in assignments)
        {
            for (var i = 0; i < a.RequiredPeriods; i++)
            {
                var day = Rng.Next(1, 7);
                var period = Rng.Next(1, 9);
                var room = rooms[Rng.Next(rooms.Count)];
                t.Classes.Add(new ScheduledClass
                {
                    AssignmentId = a.Id,
                    GroupId = a.GroupId,
                    InstructorId = a.InstructorId,
                    DisciplineId = a.DisciplineId,
                    RoomId = room.Id,
                    Slot = new TimeSlot(day, period)
                });
            }
        }

        repair.Repair(t);
        return t;
    }

    public Timetable TournamentSelect(List<Timetable> population, int tournamentSize)
    {
        var best = population[Rng.Next(population.Count)];
        for (var i = 1; i < tournamentSize; i++)
        {
            var candidate = population[Rng.Next(population.Count)];
            if ((candidate.Metrics?.F ?? 0) > (best.Metrics?.F ?? 0))
                best = candidate;
        }

        return best;
    }

    public Timetable TwoPointCrossover(Timetable parent1, Timetable parent2)
    {
        var child = new Timetable();
        var n = Math.Min(parent1.Classes.Count, parent2.Classes.Count);
        if (n == 0) return parent1.DeepClone();

        var p1 = Rng.Next(n);
        var p2 = Rng.Next(n);
        if (p1 > p2) (p1, p2) = (p2, p1);

        for (var i = 0; i < n; i++)
        {
            var src = i < p1 || i > p2 ? parent1.Classes[i] : parent2.Classes[i];
            child.Classes.Add(new ScheduledClass
            {
                AssignmentId = src.AssignmentId,
                GroupId = src.GroupId,
                InstructorId = src.InstructorId,
                DisciplineId = src.DisciplineId,
                RoomId = src.RoomId,
                Slot = src.Slot
            });
        }

        return child;
    }

    public void UniformSwapMutation(Timetable t, double probability)
    {
        if (t.Classes.Count < 2) return;
        if (Rng.NextDouble() > probability) return;
        var i1 = Rng.Next(t.Classes.Count);
        var i2 = Rng.Next(t.Classes.Count);
        (t.Classes[i1].Slot, t.Classes[i2].Slot) = (t.Classes[i2].Slot, t.Classes[i1].Slot);
    }
}