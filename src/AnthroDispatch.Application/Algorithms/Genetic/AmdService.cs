using System.Diagnostics;
using AnthroDispatch.Application.Algorithms.Awm;
using AnthroDispatch.Application.Algorithms.Cpc;
using AnthroDispatch.Application.Algorithms.Objective;
using AnthroDispatch.Application.Algorithms.Repair;
using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.Enums;
using AnthroDispatch.Domain.Metrics;
using AnthroDispatch.Domain.ValueObjects;

namespace AnthroDispatch.Application.Algorithms.Genetic;

public sealed class AmdService
{
    private readonly GaCore _core;
    private readonly GaOptions _options;
    private readonly ObjectiveFunctionService _objFn;
    private readonly DayWiseCpcCrossover _cpc;
    private readonly AwmMutation _awm;
    private readonly List<AcademicGroup> _groups;
    private readonly List<Room> _rooms;
    private readonly List<TeachingAssignment> _assignments;
    private readonly Random _rng;

    public AmdService(
        List<AcademicGroup> groups,
        List<Instructor> instructors,
        List<Discipline> disciplines,
        List<Room> rooms,
        List<TeachingAssignment> assignments,
        List<CognitiveCompatibility> compatibilities,
        ObjectiveFunctionService objFn,
        RepairService repair,
        GaOptions options)
    {
        _options = options;
        _objFn = objFn;
        _groups = groups;
        _rooms = rooms;
        _assignments = assignments;
        _rng = new Random(options.Seed);
        _core = new GaCore(groups, instructors, disciplines, rooms, assignments, compatibilities, objFn, repair,
            options.Seed);
        _cpc = new DayWiseCpcCrossover(groups, instructors, repair, options.CpcGamma, _rng);
        _awm = new AwmMutation(groups, instructors, compatibilities, repair, options.AwmBeta, _rng, disciplines);
    }

    public OptimizationResult Run(ObjectiveWeights weights)
    {
        var sw = Stopwatch.StartNew();
        var population = Enumerable.Range(0, _options.PopulationSize)
            .Select(_ => GreedyInitialize()).ToList();

        foreach (var t in population) _objFn.Evaluate(t, weights);

        var history = new List<double>();
        double timeToF075 = -1;
        double timeToF065 = -1;
        var eliteCount = Math.Max(1, (int)(_options.PopulationSize * _options.EliteFraction));
        double prevBest = 0;
        var stagnationCount = 0;

        for (var gen = 0; gen < _options.MaxGenerations; gen++)
        {
            population = population.OrderByDescending(t => t.Metrics!.F).ToList();
            var best = population[0].Metrics!.F;
            history.Add(best);

            if (timeToF075 < 0 && best >= 0.75) timeToF075 = sw.Elapsed.TotalSeconds;
            if (timeToF065 < 0 && best >= 0.65) timeToF065 = sw.Elapsed.TotalSeconds;
            if (Math.Abs(best - prevBest) < _options.StagnationThreshold) stagnationCount++;
            else stagnationCount = 0;
            prevBest = best;
            if (stagnationCount >= _options.StagnationGenerations) break;

            var elites = population.Take(eliteCount).Select(t => t.DeepClone()).ToList();
            var newPop = new List<Timetable>(elites);

            while (newPop.Count < _options.PopulationSize)
            {
                var p1 = _core.TournamentSelect(population, _options.TournamentSize);
                var p2 = _core.TournamentSelect(population, _options.TournamentSize);

                var child = _rng.NextDouble() < _options.CrossoverProbability ? _cpc.Crossover(p1, p2) : p1.DeepClone();

                if (_rng.NextDouble() < _options.MutationProbability)
                    _awm.Mutate(child, weights);

                _objFn.Evaluate(child, weights);
                newPop.Add(child);
            }

            population = newPop;
        }

        population = population.OrderByDescending(t => t.Metrics!.F).ToList();
        var bestTimetable = population[0];
        var topCandidates = population.Take(_options.TopMCandidates).ToList();
        return new OptimizationResult(
            bestTimetable,
            bestTimetable.Metrics!,
            history,
            history.Count,
            timeToF075 < 0 ? sw.Elapsed.TotalSeconds : timeToF075,
            timeToF065 < 0 ? sw.Elapsed.TotalSeconds : timeToF065,
            topCandidates);
    }

    private Timetable GreedyInitialize()
    {
        var t = new Timetable();
        foreach (var a in _assignments)
        {
            for (var i = 0; i < a.RequiredPeriods; i++)
            {
                // Try each chronotype-preferred slot first
                var group = _groups.FirstOrDefault(g => g.Id == a.GroupId);
                var preferredPeriods = group != null
                    ? GetPreferredPeriods(group.Chronotype)
                    : [1, 2, 3, 4, 5, 6, 7, 8];

                var day = _rng.Next(1, 7);
                var period = preferredPeriods[_rng.Next(preferredPeriods.Length)];
                var room = _rooms[_rng.Next(_rooms.Count)];

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

        return t;
    }

    private static int[] GetPreferredPeriods(ChronotypeCategory c) => c switch
    {
        ChronotypeCategory.DefiniteMorning => [1, 2, 3],
        ChronotypeCategory.ModerateMorning => [2, 3, 4],
        ChronotypeCategory.Intermediate => [3, 4, 5],
        ChronotypeCategory.ModerateEvening => [5, 6, 7],
        ChronotypeCategory.DefiniteEvening => [6, 7, 8],
        _ => [3, 4, 5]
    };
}