using System.Diagnostics;
using AnthroDispatch.Application.Algorithms.Objective;
using AnthroDispatch.Application.Algorithms.Repair;
using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.Metrics;

namespace AnthroDispatch.Application.Algorithms.Genetic;

/// <summary>
/// A standard, published multi-objective baseline (Deb et al., NSGA-II) for the ablation study,
/// requested by article peer review as an external comparator beyond the internal Baseline
/// GA/CPC-GA/AWM-GA/AMD family. Unlike those, NSGA-II optimizes the four raw objective-function
/// components (F_tech, F_circ, F_psych, F_cogn) directly as a Pareto front, using the same
/// two-point crossover / uniform-swap mutation operators as BaselineGaService so the comparison
/// isolates the effect of the selection mechanism (non-dominated sorting + crowding distance)
/// rather than the reproduction operators. For reporting alongside the scalarized F(x,w) metrics
/// in the ablation table, the front-0 member maximizing the scalarized F(x,w) is returned as
/// "the" result each generation/at the end — NSGA-II itself never optimizes this scalar directly.
/// </summary>
public sealed class NsgaIIService(
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
        double prevBest = 0;
        var stagnationCount = 0;

        for (var gen = 0; gen < options.MaxGenerations; gen++)
        {
            var best = population.Max(t => t.Metrics!.F);
            history.Add(best);

            if (timeToF075 < 0 && best >= 0.75) timeToF075 = sw.Elapsed.TotalSeconds;
            if (timeToF065 < 0 && best >= 0.65) timeToF065 = sw.Elapsed.TotalSeconds;

            if (Math.Abs(best - prevBest) < options.StagnationThreshold) stagnationCount++;
            else stagnationCount = 0;
            prevBest = best;
            if (stagnationCount >= options.StagnationGenerations) break;

            var (rank, crowding) = RankAndCrowd(population);

            var offspring = new List<Timetable>(options.PopulationSize);
            while (offspring.Count < options.PopulationSize)
            {
                var p1 = CrowdedTournamentSelect(population, rank, crowding);
                var child = _core.Rng.NextDouble() < options.CrossoverProbability
                    ? _core.TwoPointCrossover(p1, CrowdedTournamentSelect(population, rank, crowding))
                    : p1.DeepClone();
                _core.UniformSwapMutation(child, options.MutationProbability);
                objFn.Evaluate(child, weights);
                offspring.Add(child);
            }

            population = EnvironmentalSelection(population.Concat(offspring).ToList(), options.PopulationSize);
        }

        var finalFronts = FastNonDominatedSort(population);
        var paretoFront = finalFronts[0];
        // NSGA-II optimizes the 4-objective Pareto front, not the scalar F(x,w); the front-0
        // member maximizing F(x,w) is reported so the result slots into the same ablation-table
        // format (F100/F500/etc.) as the scalarized baselines — see class remarks above.
        var bestTimetable = paretoFront.OrderByDescending(t => t.Metrics!.F).First();
        var topCandidates = population.OrderByDescending(t => t.Metrics!.F).Take(options.TopMCandidates).ToList();

        return new OptimizationResult(
            bestTimetable,
            bestTimetable.Metrics!,
            history,
            history.Count,
            timeToF075 < 0 ? sw.Elapsed.TotalSeconds : timeToF075,
            timeToF065 < 0 ? sw.Elapsed.TotalSeconds : timeToF065,
            topCandidates);
    }

    // ── NSGA-II machinery (Deb et al. 2002) ─────────────────────────────────

    private static double[] Objectives(Timetable t)
    {
        var m = t.Metrics!;
        return [m.FTech, m.FCirc, m.FPsych, m.FCogn]; // all four maximized
    }

    private static bool Dominates(Timetable a, Timetable b)
    {
        var oa = Objectives(a);
        var ob = Objectives(b);
        var strictlyBetterInOne = false;
        for (var i = 0; i < oa.Length; i++)
        {
            if (oa[i] < ob[i]) return false;
            if (oa[i] > ob[i]) strictlyBetterInOne = true;
        }

        return strictlyBetterInOne;
    }

    private static List<List<Timetable>> FastNonDominatedSort(List<Timetable> pop)
    {
        var dominatedBy = new Dictionary<Timetable, List<Timetable>>();
        var dominationCount = new Dictionary<Timetable, int>();
        var fronts = new List<List<Timetable>> { new List<Timetable>() };

        foreach (var p in pop)
        {
            var dominated = new List<Timetable>();
            var count = 0;
            foreach (var q in pop)
            {
                if (ReferenceEquals(p, q)) continue;
                if (Dominates(p, q)) dominated.Add(q);
                else if (Dominates(q, p)) count++;
            }

            dominatedBy[p] = dominated;
            dominationCount[p] = count;
            if (count == 0) fronts[0].Add(p);
        }

        var i = 0;
        while (fronts[i].Count > 0)
        {
            var next = new List<Timetable>();
            foreach (var p in fronts[i])
            foreach (var q in dominatedBy[p])
            {
                dominationCount[q]--;
                if (dominationCount[q] == 0) next.Add(q);
            }

            i++;
            fronts.Add(next);
        }

        fronts.RemoveAt(fronts.Count - 1);
        return fronts;
    }

    private static Dictionary<Timetable, double> CrowdingDistance(List<Timetable> front)
    {
        var distance = front.ToDictionary(t => t, _ => 0.0);
        if (front.Count <= 2)
        {
            foreach (var t in front) distance[t] = double.PositiveInfinity;
            return distance;
        }

        for (var m = 0; m < 4; m++)
        {
            var sorted = front.OrderBy(t => Objectives(t)[m]).ToList();
            distance[sorted[0]] = double.PositiveInfinity;
            distance[sorted[^1]] = double.PositiveInfinity;
            var min = Objectives(sorted[0])[m];
            var max = Objectives(sorted[^1])[m];
            var range = max - min;
            if (range <= 1e-12) continue;

            for (var k = 1; k < sorted.Count - 1; k++)
            {
                if (double.IsPositiveInfinity(distance[sorted[k]])) continue;
                distance[sorted[k]] += (Objectives(sorted[k + 1])[m] - Objectives(sorted[k - 1])[m]) / range;
            }
        }

        return distance;
    }

    private static (Dictionary<Timetable, int> rank, Dictionary<Timetable, double> crowding) RankAndCrowd(
        List<Timetable> pop)
    {
        var fronts = FastNonDominatedSort(pop);
        var rank = new Dictionary<Timetable, int>();
        var crowding = new Dictionary<Timetable, double>();
        for (var f = 0; f < fronts.Count; f++)
        {
            var d = CrowdingDistance(fronts[f]);
            foreach (var t in fronts[f])
            {
                rank[t] = f;
                crowding[t] = d[t];
            }
        }

        return (rank, crowding);
    }

    private Timetable CrowdedTournamentSelect(
        List<Timetable> pop, Dictionary<Timetable, int> rank, Dictionary<Timetable, double> crowding)
    {
        var a = pop[_core.Rng.Next(pop.Count)];
        var b = pop[_core.Rng.Next(pop.Count)];
        if (rank[a] != rank[b]) return rank[a] < rank[b] ? a : b;
        return crowding[a] >= crowding[b] ? a : b;
    }

    private static List<Timetable> EnvironmentalSelection(List<Timetable> combined, int targetSize)
    {
        var fronts = FastNonDominatedSort(combined);
        var next = new List<Timetable>(targetSize);
        foreach (var front in fronts)
        {
            if (next.Count + front.Count <= targetSize)
            {
                next.AddRange(front);
            }
            else
            {
                var distance = CrowdingDistance(front);
                var remaining = targetSize - next.Count;
                next.AddRange(front.OrderByDescending(t => distance[t]).Take(remaining));
                break;
            }

            if (next.Count == targetSize) break;
        }

        return next;
    }
}
