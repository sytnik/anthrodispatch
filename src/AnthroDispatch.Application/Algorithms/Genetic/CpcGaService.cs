using System.Diagnostics;
using AnthroDispatch.Application.Algorithms.Cpc;
using AnthroDispatch.Application.Algorithms.Objective;
using AnthroDispatch.Application.Algorithms.Repair;
using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.Metrics;

namespace AnthroDispatch.Application.Algorithms.Genetic;

/// <summary>Baseline GA + CPC crossover only (no AWM).</summary>
public sealed class CpcGaService
{
    private readonly GaCore _core;
    private readonly GaOptions _options;
    private readonly ObjectiveFunctionService _objFn;
    private readonly DayWiseCpcCrossover _cpc;
    private readonly Random _rng;

    public CpcGaService(
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
        _rng = new Random(options.Seed);
        _core = new GaCore(groups, instructors, disciplines, rooms, assignments, compatibilities, objFn, repair,
            options.Seed);
        _cpc = new DayWiseCpcCrossover(groups, instructors, repair, options.CpcGamma, _rng);
    }

    public OptimizationResult Run(ObjectiveWeights weights)
    {
        var sw = Stopwatch.StartNew();
        var population = Enumerable.Range(0, _options.PopulationSize)
            .Select(_ => _core.RandomInitialize()).ToList();
        foreach (var t in population) _objFn.Evaluate(t, weights);

        var history = new List<double>();
        double timeToF075 = -1;
        double timeToF065 = -1;
        var eliteCount = Math.Max(1, (int)(_options.PopulationSize * _options.EliteFraction));
        double prevBest = 0;
        var stagnation = 0;

        for (var gen = 0; gen < _options.MaxGenerations; gen++)
        {
            population = population.OrderByDescending(t => t.Metrics!.F).ToList();
            var best = population[0].Metrics!.F;
            history.Add(best);
            if (timeToF075 < 0 && best >= 0.75) timeToF075 = sw.Elapsed.TotalSeconds;
            if (timeToF065 < 0 && best >= 0.65) timeToF065 = sw.Elapsed.TotalSeconds;
            if (Math.Abs(best - prevBest) < _options.StagnationThreshold) stagnation++;
            else stagnation = 0;
            prevBest = best;
            if (stagnation >= _options.StagnationGenerations) break;

            var elites = population.Take(eliteCount).Select(t => t.DeepClone()).ToList();
            var newPop = new List<Timetable>(elites);
            while (newPop.Count < _options.PopulationSize)
            {
                var p1 = _core.TournamentSelect(population, _options.TournamentSize);
                var child = _rng.NextDouble() < _options.CrossoverProbability
                    ? _cpc.Crossover(p1, _core.TournamentSelect(population, _options.TournamentSize))
                    : p1.DeepClone();
                _core.UniformSwapMutation(child, _options.MutationProbability);
                _objFn.Evaluate(child, weights);
                newPop.Add(child);
            }

            population = newPop;
        }

        population = population.OrderByDescending(t => t.Metrics!.F).ToList();
        var bestTimetable = population[0];
        var topCandidates = population.Take(_options.TopMCandidates).ToList();
        return new OptimizationResult(bestTimetable, bestTimetable.Metrics!, history, history.Count,
            timeToF075 < 0 ? sw.Elapsed.TotalSeconds : timeToF075,
            timeToF065 < 0 ? sw.Elapsed.TotalSeconds : timeToF065,
            topCandidates);
    }
}