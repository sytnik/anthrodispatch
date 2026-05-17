using System.Diagnostics;
using AnthroDispatch.Application.Algorithms.Objective;
using AnthroDispatch.Application.Algorithms.Repair;
using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.Metrics;

namespace AnthroDispatch.Application.Algorithms.Genetic;

public sealed class BaselineGaService(
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
    private readonly GaCore _core = new(groups, instructors, disciplines, rooms, assignments, compatibilities, objFn,
        repair, options.Seed);

    public OptimizationResult Run(ObjectiveWeights weights)
    {
        var sw = Stopwatch.StartNew();
        var population = Enumerable.Range(0, options.PopulationSize)
            .Select(_ => _core.RandomInitialize()).ToList();

        foreach (var t in population) objFn.Evaluate(t, weights);

        var history = new List<double>();
        double timeToF075 = -1;
        double timeToF065 = -1;
        var eliteCount = Math.Max(1, (int)(options.PopulationSize * options.EliteFraction));
        double prevBest = 0;
        var stagnationCount = 0;

        for (var gen = 0; gen < options.MaxGenerations; gen++)
        {
            population = population.OrderByDescending(t => t.Metrics!.F).ToList();
            var best = population[0].Metrics!.F;
            history.Add(best);

            if (timeToF075 < 0 && best >= 0.75) timeToF075 = sw.Elapsed.TotalSeconds;
            if (timeToF065 < 0 && best >= 0.65) timeToF065 = sw.Elapsed.TotalSeconds;

            if (Math.Abs(best - prevBest) < options.StagnationThreshold) stagnationCount++;
            else stagnationCount = 0;
            prevBest = best;
            if (stagnationCount >= options.StagnationGenerations) break;

            var elites = population.Take(eliteCount).Select(t => t.DeepClone()).ToList();
            var newPop = new List<Timetable>(elites);

            while (newPop.Count < options.PopulationSize)
            {
                var p1 = _core.TournamentSelect(population, options.TournamentSize);
                var child = _core.Rng.NextDouble() < options.CrossoverProbability
                    ? _core.TwoPointCrossover(p1, _core.TournamentSelect(population, options.TournamentSize))
                    : p1.DeepClone();
                _core.UniformSwapMutation(child, options.MutationProbability);
                objFn.Evaluate(child, weights);
                newPop.Add(child);
            }

            population = newPop;
        }

        population = population.OrderByDescending(t => t.Metrics!.F).ToList();
        var bestTimetable = population[0];
        return new OptimizationResult(
            bestTimetable,
            bestTimetable.Metrics!,
            history,
            history.Count,
            timeToF075 < 0 ? sw.Elapsed.TotalSeconds : timeToF075,
            timeToF065 < 0 ? sw.Elapsed.TotalSeconds : timeToF065);
    }
}