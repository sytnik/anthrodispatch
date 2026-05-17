using AnthroDispatch.Application.Algorithms.Objective;
using AnthroDispatch.Application.Algorithms.Sra;
using AnthroDispatch.Domain.Metrics;
using AnthroDispatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AnthroDispatch.Api.Endpoints;

public static class SraEndpoints
{
    public static void MapSraEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/sra/adapt", async (
            SraAdaptRequest req,
            AnthroDispatchDbContext db,
            CancellationToken ct) =>
        {
            var run = await db.OptimizationRuns.FindAsync([req.RunId], cancellationToken: ct);
            if (run is null) return Results.NotFound("Run not found.");

            var groups = await db.Groups.ToListAsync(ct);
            var instructors = await db.Instructors.ToListAsync(ct);
            var disciplines = await db.Disciplines.ToListAsync(ct);
            var rooms = await db.Rooms.ToListAsync(ct);
            var assignments = await db.Assignments.ToListAsync(ct);
            var compatibilities = await db.CognitiveCompatibilities.ToListAsync(ct);

            // Generate participant timetable samples by running quick evaluations on random timetables
            var objFn = new ObjectiveFunctionService(groups, instructors, disciplines, rooms, assignments,
                compatibilities);
            var weights = new ObjectiveWeights { Tech = 0.25, Circ = 0.25, Psych = 0.25, Cogn = 0.25 };
            var rng = new Random(req.Seed);

            // Use the stored run's metrics as the base; generate slight variations for participants
            var samples = Enumerable.Range(0, req.Participants).Select(_ => new TimetableMetrics
            {
                FTech = Math.Clamp(run.FTech + (rng.NextDouble() - 0.5) * 0.1, 0, 1),
                FCirc = Math.Clamp(run.FCirc + (rng.NextDouble() - 0.5) * 0.1, 0, 1),
                FPsych = Math.Clamp(run.FPsych + (rng.NextDouble() - 0.5) * 0.1, 0, 1),
                FCogn = Math.Clamp(run.FCogn + (rng.NextDouble() - 0.5) * 0.1, 0, 1),
            }).ToList();

            var oldWeights = new ObjectiveWeights
            {
                Tech = req.OldWeights?.Tech ?? 0.25,
                Circ = req.OldWeights?.Circ ?? 0.25,
                Psych = req.OldWeights?.Psych ?? 0.25,
                Cogn = req.OldWeights?.Cogn ?? 0.25
            };

            var sraResult = new SraService().Adapt(samples, oldWeights, req.Seed);

            return Results.Ok(new
            {
                oldWeights = new
                {
                    tech = sraResult.OldWeights.Tech, circ = sraResult.OldWeights.Circ,
                    psych = sraResult.OldWeights.Psych, cogn = sraResult.OldWeights.Cogn
                },
                newWeights = new
                {
                    tech = sraResult.NewWeights.Tech, circ = sraResult.NewWeights.Circ,
                    psych = sraResult.NewWeights.Psych, cogn = sraResult.NewWeights.Cogn
                },
                distanceToReference = sraResult.DistanceToReference,
                correlationToReference = sraResult.CorrelationToReference
            });
        }).WithName("SraAdapt").WithTags("SRA");
    }
}