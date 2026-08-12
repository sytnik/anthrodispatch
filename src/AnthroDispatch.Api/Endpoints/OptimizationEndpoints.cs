using System.Text.Json;
using AnthroDispatch.Application.Algorithms.Explanation;
using AnthroDispatch.Application.Algorithms.Genetic;
using AnthroDispatch.Application.Algorithms.Objective;
using AnthroDispatch.Application.Algorithms.Repair;
using AnthroDispatch.Application.Algorithms.ScoreIa;
using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.Enums;
using AnthroDispatch.Domain.Metrics;
using AnthroDispatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AnthroDispatch.Api.Endpoints;

public static class OptimizationEndpoints
{
    public static void MapOptimizationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/optimization/run", async (
            RunOptimizationRequest req,
            AnthroDispatchDbContext db,
            DispatchProblemCache problemCache,
            CancellationToken ct) =>
        {
            List<AcademicGroup> groups;
            List<Instructor> instructors;
            List<Discipline> disciplines;
            List<Room> rooms;
            List<TeachingAssignment> assignments;
            List<CognitiveCompatibility> compatibilities;

            // pipeline: prefer dispatchProblemId → load from cache
            if (req.DispatchProblemId.HasValue && problemCache.Contains(req.DispatchProblemId.Value))
            {
                var problem = problemCache.Get(req.DispatchProblemId.Value)!;
                groups = problem.Groups.ToList();
                instructors = problem.Instructors.ToList();
                disciplines = problem.Disciplines.ToList();
                rooms = problem.Rooms.ToList();
                compatibilities = problem.CognitiveCompatibilityMatrix.ToList();

                // Bridge AtomicSchedulingUnits → legacy TeachingAssignment for the GA core
                assignments = problem.AtomicUnits.Select(u => new TeachingAssignment
                {
                    Id = u.Id,
                    GroupId = u.GroupIds.FirstOrDefault(),
                    InstructorId = u.InstructorIds.FirstOrDefault(),
                    DisciplineId = u.DisciplineId,
                    ClassType = u.LessonType switch
                    {
                        LessonType.Laboratory => ClassType.Laboratory,
                        LessonType.Practice => ClassType.Practice,
                        LessonType.Seminar => ClassType.Seminar,
                        LessonType.Online => ClassType.Online,
                        _ => ClassType.Lecture
                    },
                    RequiredPeriods = Math.Clamp(u.RequiredPeriods, 1, 6)
                }).Where(a => a.GroupId != Guid.Empty && a.DisciplineId != Guid.Empty).ToList();
            }
            else
            {
                // Legacy fallback: load from InMemory DB (filtered by datasetId where possible)
                groups = await db.Groups.ToListAsync(ct);
                instructors = await db.Instructors.ToListAsync(ct);
                disciplines = await db.Disciplines.ToListAsync(ct);
                rooms = await db.Rooms.ToListAsync(ct);
                assignments = await db.Assignments.ToListAsync(ct);
                compatibilities = await db.CognitiveCompatibilities.ToListAsync(ct);
            }

            if (groups.Count == 0) return Results.BadRequest("No dataset found. Generate a dataset first.");

            var weights = new ObjectiveWeights
            {
                Tech = req.Weights?.Tech ?? 0.25,
                Circ = req.Weights?.Circ ?? 0.25,
                Psych = req.Weights?.Psych ?? 0.25,
                Cogn = req.Weights?.Cogn ?? 0.25
            };

            var objFn = new ObjectiveFunctionService(groups, instructors, disciplines, rooms, assignments,
                compatibilities);
            var repair = new RepairService(rooms, instructors);
            var options = new GaOptions
            {
                PopulationSize = req.PopulationSize,
                MaxGenerations = req.MaxGenerations,
                Seed = req.Seed
            };

            var result = req.Algorithm.ToUpperInvariant() switch
            {
                "AMD" => new AmdService(groups, instructors, disciplines, rooms, assignments, compatibilities, objFn,
                    repair, options).Run(weights),
                "CPCGA" => new CpcGaService(groups, instructors, disciplines, rooms, assignments, compatibilities,
                    objFn, repair, options).Run(weights),
                "AWMGA" => new AwmGaService(groups, instructors, disciplines, rooms, assignments, compatibilities,
                    objFn, repair, options).Run(weights),
                _ => new BaselineGaService(groups, instructors, disciplines, rooms, assignments, compatibilities, objFn,
                    repair, options).Run(weights)
            };

            // X_cand ranked by Score_IA (§2.4) — computed here while
            // groups/instructors/disciplines/compatibilities are already in
            // memory; no "previous approved version" concept exists in this
            // request flow yet, so FStable is scored against previous=null
            // (ScoreIaService treats that as fully stable — nothing to
            // destabilise on a first-ever dispatch).
            var explanationSvc = new ExplanationService(groups, instructors, disciplines, compatibilities);
            var scoreIaSvc = new ScoreIaService(explanationSvc);
            var ranked = scoreIaSvc.RankCandidates(result.TopCandidates ?? [result.BestTimetable]);
            var candidatesJson = JsonSerializer.Serialize(ranked.Select((r, i) => new RankedCandidateDto(
                Rank: i + 1,
                TimetableId: r.Timetable.Id,
                Classes: r.Timetable.Classes.Select(c => new ScheduledClassDto(
                    c.Id, c.AssignmentId, c.GroupId, c.InstructorId, c.DisciplineId, c.RoomId,
                    c.Slot.Day, c.Slot.Period)).ToList(),
                FTech: r.Z.FTech,
                FCirc: r.Z.FCirc,
                FPsych: r.Z.FPsych,
                FCogn: r.Z.FCogn,
                FStable: r.Z.FStable,
                Risk: r.Z.Risk,
                Explainability: r.Z.Explainability,
                ScoreIa: r.ScoreIa)));

            var run = new OptimizationRun
            {
                DatasetId = req.DatasetId,
                DispatchProblemId = req.DispatchProblemId,
                Algorithm = req.Algorithm,
                BestFitness = result.BestMetrics.F,
                FTech = result.BestMetrics.FTech,
                FCirc = result.BestMetrics.FCirc,
                FPsych = result.BestMetrics.FPsych,
                FCogn = result.BestMetrics.FCogn,
                Conflicts = result.BestMetrics.Conflicts,
                Generations = result.GenerationsRun,
                TimeToF075Seconds = result.TimeToF075Seconds,
                TimetableJson = JsonSerializer.Serialize(result.BestTimetable.Classes.Select(c => new
                {
                    c.Id, c.AssignmentId, c.GroupId, c.InstructorId, c.DisciplineId, c.RoomId,
                    c.Slot.Day,
                    c.Slot.Period
                })),
                CandidatesJson = candidatesJson
            };
            await db.OptimizationRuns.AddAsync(run, ct);
            await db.SaveChangesAsync(ct);

            return Results.Ok(new
            {
                runId = run.Id,
                algorithm = run.Algorithm,
                bestFitness = run.BestFitness,
                fTech = run.FTech,
                fCirc = run.FCirc,
                fPsych = run.FPsych,
                fCogn = run.FCogn,
                conflicts = run.Conflicts,
                generations = run.Generations,
                timeToF075Seconds = run.TimeToF075Seconds,
                candidatesRanked = ranked.Count
            });
        }).WithName("RunOptimization").WithTags("Optimization");

        app.MapGet("/api/optimization/{runId:guid}/timetable", async (Guid runId, AnthroDispatchDbContext db) =>
        {
            var run = await db.OptimizationRuns.FindAsync(runId);
            return run is null ? Results.NotFound() : Results.Ok(new { runId, timetableJson = run.TimetableJson });
        }).WithName("GetTimetable").WithTags("Optimization");

        app.MapGet("/api/optimization/{runId:guid}/metrics", async (Guid runId, AnthroDispatchDbContext db) =>
        {
            var run = await db.OptimizationRuns.FindAsync(runId);
            if (run is null) return Results.NotFound();
            return Results.Ok(new
            {
                runId,
                run.BestFitness,
                run.FTech,
                run.FCirc,
                run.FPsych,
                run.FCogn,
                run.Conflicts,
                run.Generations,
                run.TimeToF075Seconds
            });
        }).WithName("GetMetrics").WithTags("Optimization");
    }
}