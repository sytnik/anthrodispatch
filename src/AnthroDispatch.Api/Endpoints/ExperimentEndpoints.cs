using AnthroDispatch.Application.Algorithms.Genetic;
using AnthroDispatch.Application.Algorithms.Objective;
using AnthroDispatch.Application.Algorithms.Repair;
using AnthroDispatch.Domain.Metrics;
using AnthroDispatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AnthroDispatch.Api.Endpoints;

public static class ExperimentEndpoints
{
    public static void MapExperimentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/experiments/ablation", async (
            AblationRequest req,
            AnthroDispatchDbContext db,
            CancellationToken ct) =>
        {
            var groups = await db.Groups.ToListAsync(ct);
            var instructors = await db.Instructors.ToListAsync(ct);
            var disciplines = await db.Disciplines.ToListAsync(ct);
            var rooms = await db.Rooms.ToListAsync(ct);
            var assignments = await db.Assignments.ToListAsync(ct);
            var compatibilities = await db.CognitiveCompatibilities.ToListAsync(ct);

            if (groups.Count == 0) return Results.BadRequest("No dataset found. Generate a dataset first.");

            var weights = ObjectiveWeights.Default;
            var results = new List<object>();

            var algorithms = new[] { "BaselineGA", "CpcGA", "AwmGA", "AMD" };
            foreach (var algoName in algorithms)
            {
                var fitnesses = new List<double>();
                var f100List = new List<double>();
                var f500List = new List<double>();
                var tToF075 = new List<double>();

                for (var run = 0; run < req.Runs; run++)
                {
                    var objFn = new ObjectiveFunctionService(groups, instructors, disciplines, rooms, assignments,
                        compatibilities);
                    var repair = new RepairService(rooms, instructors);
                    var opts = new GaOptions
                    {
                        PopulationSize = req.PopulationSize,
                        MaxGenerations = req.MaxGenerations,
                        Seed = req.Seed + run
                    };

                    var optResult = algoName switch
                    {
                        "AMD" => new AmdService(groups, instructors, disciplines, rooms, assignments, compatibilities,
                            objFn, repair, opts).Run(weights),
                        "CpcGA" => new CpcGaService(groups, instructors, disciplines, rooms, assignments,
                            compatibilities, objFn, repair, opts).Run(weights),
                        "AwmGA" => new AwmGaService(groups, instructors, disciplines, rooms, assignments,
                            compatibilities, objFn, repair, opts).Run(weights),
                        _ => new BaselineGaService(groups, instructors, disciplines, rooms, assignments,
                            compatibilities, objFn, repair, opts).Run(weights)
                    };

                    fitnesses.Add(optResult.BestMetrics.F);
                    // F100: fitness at generation 100 (or last if fewer)
                    f100List.Add(optResult.FitnessHistory.Count >= 100
                        ? optResult.FitnessHistory[99]
                        : optResult.BestMetrics.F);
                    // F500: fitness at generation 500 (or last if fewer)
                    f500List.Add(optResult.FitnessHistory.Count >= 500
                        ? optResult.FitnessHistory[499]
                        : optResult.BestMetrics.F);
                    tToF075.Add(optResult.TimeToF075Seconds / 60.0);
                }

                var std = StdDev(fitnesses);
                results.Add(new
                {
                    algorithm = algoName,
                    f100Mean = f100List.Average(),
                    f500Mean = f500List.Average(),
                    timeToF075MeanMinutes = tToF075.Where(x => x >= 0).DefaultIfEmpty(0).Average(),
                    finalStd = std
                });
            }

            return Results.Ok(new { results });
        }).WithName("AblationExperiment").WithTags("Experiments");
    }

    private static double StdDev(List<double> values)
    {
        if (values.Count <= 1) return 0;
        var mean = values.Average();
        return Math.Sqrt(values.Average(v => (v - mean) * (v - mean)));
    }
}