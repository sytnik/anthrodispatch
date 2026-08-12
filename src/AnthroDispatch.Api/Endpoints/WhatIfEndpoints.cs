using System.Text.Json;
using AnthroDispatch.Application.Algorithms.Explanation;
using AnthroDispatch.Application.Algorithms.Objective;
using AnthroDispatch.Application.Algorithms.Repair;
using AnthroDispatch.Application.Algorithms.WhatIf;
using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.Metrics;
using AnthroDispatch.Domain.ValueObjects;
using AnthroDispatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AnthroDispatch.Api.Endpoints;

public static class WhatIfEndpoints
{
    public static void MapWhatIfEndpoints(this IEndpointRouteBuilder app)
    {
        // Scenario 1
        app.MapPost("/api/whatif/instructor-unavailable", async (
            InstructorUnavailableRequest req, AnthroDispatchDbContext db, CancellationToken ct) =>
        {
            var (svc, weights, original) = await BuildContext(req.RunId, db, ct);
            if (svc is null) return Results.NotFound("Run not found.");
            var result = svc.InstructorUnavailable(original!, weights!, req.InstructorId, req.Day, req.Period);
            return Results.Ok(ToResponse(result));
        }).WithName("WhatIfInstructorUnavailable").WithTags("WhatIf");

        // Scenario 2
        app.MapPost("/api/whatif/room-unavailable", async (
            RoomUnavailableRequest req, AnthroDispatchDbContext db, CancellationToken ct) =>
        {
            var (svc, weights, original) = await BuildContext(req.RunId, db, ct);
            if (svc is null) return Results.NotFound("Run not found.");
            var rooms = await db.Rooms.ToListAsync(ct);
            var result = svc.RoomUnavailable(original!, weights!, req.RoomId, req.Day, rooms);
            return Results.Ok(ToResponse(result));
        }).WithName("WhatIfRoomUnavailable").WithTags("WhatIf");

        // Scenario 3
        app.MapPost("/api/whatif/group-unavailable", async (
            GroupUnavailableRequest req, AnthroDispatchDbContext db, CancellationToken ct) =>
        {
            var (svc, weights, original) = await BuildContext(req.RunId, db, ct);
            if (svc is null) return Results.NotFound("Run not found.");
            var result = svc.GroupUnavailable(original!, weights!, req.GroupId, req.Day, req.Period);
            return Results.Ok(ToResponse(result));
        }).WithName("WhatIfGroupUnavailable").WithTags("WhatIf");

        // Scenario 4
        app.MapPost("/api/whatif/discipline-moved", async (
            DisciplineMovedRequest req, AnthroDispatchDbContext db, CancellationToken ct) =>
        {
            var (svc, weights, original) = await BuildContext(req.RunId, db, ct);
            if (svc is null) return Results.NotFound("Run not found.");
            var result = svc.DisciplineMoved(original!, weights!, req.DisciplineId, req.TargetDay, req.TargetPeriod);
            return Results.Ok(ToResponse(result));
        }).WithName("WhatIfDisciplineMoved").WithTags("WhatIf");

        // Scenario 5
        app.MapPost("/api/whatif/weights-changed", async (
            WeightsChangedRequest req, AnthroDispatchDbContext db, CancellationToken ct) =>
        {
            var (svc, _, original) = await BuildContext(req.RunId, db, ct);
            if (svc is null) return Results.NotFound("Run not found.");
            var oldW = new ObjectiveWeights
            {
                Tech = req.OldWeights.Tech, Circ = req.OldWeights.Circ, Psych = req.OldWeights.Psych,
                Cogn = req.OldWeights.Cogn
            };
            var newW = new ObjectiveWeights
            {
                Tech = req.NewWeights.Tech, Circ = req.NewWeights.Circ, Psych = req.NewWeights.Psych,
                Cogn = req.NewWeights.Cogn
            };
            var result = svc.WeightsChanged(original!, oldW, newW);
            return Results.Ok(ToResponse(result));
        }).WithName("WhatIfWeightsChanged").WithTags("WhatIf");

        // Scenario 6: Instructor constraint
        app.MapPost("/api/whatif/instructor-constraint", async (
            InstructorConstraintRequest req, AnthroDispatchDbContext db, CancellationToken ct) =>
        {
            var (svc, weights, original) = await BuildContext(req.RunId, db, ct);
            if (svc is null) return Results.NotFound("Run not found.");
            var result = svc.InstructorConstraintApplied(original!, weights!, req.InstructorId, req.ConstraintType,
                req.Day, req.Period);
            return Results.Ok(ToResponse(result));
        }).WithName("WhatIfInstructorConstraint").WithTags("WhatIf");

        // Scenario 7: Health limitation appears
        app.MapPost("/api/whatif/health-limitation", async (
            HealthLimitationRequest req, AnthroDispatchDbContext db, CancellationToken ct) =>
        {
            var (svc, weights, original) = await BuildContext(req.RunId, db, ct);
            if (svc is null) return Results.NotFound("Run not found.");
            var result = svc.HealthLimitationApplied(original!, weights!, req.LimitationType);
            return Results.Ok(ToResponse(result));
        }).WithName("WhatIfHealthLimitation").WithTags("WhatIf");

        // Scenario 8: Room capacity insufficient
        app.MapPost("/api/whatif/room-capacity", async (
            RoomCapacityRequest req, AnthroDispatchDbContext db, CancellationToken ct) =>
        {
            var (svc, weights, original) = await BuildContext(req.RunId, db, ct);
            if (svc is null) return Results.NotFound("Run not found.");
            var rooms = await db.Rooms.ToListAsync(ct);
            var result = svc.RoomCapacityInsufficient(original!, weights!, req.RoomId, req.RequiredCapacity, rooms);
            return Results.Ok(ToResponse(result));
        }).WithName("WhatIfRoomCapacity").WithTags("WhatIf");

        // Scenario 9: Group constraint
        app.MapPost("/api/whatif/group-constraint", async (
            GroupConstraintRequest req, AnthroDispatchDbContext db, CancellationToken ct) =>
        {
            var (svc, weights, original) = await BuildContext(req.RunId, db, ct);
            if (svc is null) return Results.NotFound("Run not found.");
            var result = svc.GroupConstraintApplied(original!, weights!, req.GroupId, req.ConstraintType, req.Day,
                req.Period);
            return Results.Ok(ToResponse(result));
        }).WithName("WhatIfGroupConstraint").WithTags("WhatIf");

        // Scenario 10: Mode change (online ↔ offline)
        app.MapPost("/api/whatif/mode-change", async (
            ModeChangeRequest req, AnthroDispatchDbContext db, CancellationToken ct) =>
        {
            var (svc, weights, original) = await BuildContext(req.RunId, db, ct);
            if (svc is null) return Results.NotFound("Run not found.");
            var rooms = await db.Rooms.ToListAsync(ct);
            var result = svc.ModeChanged(original!, weights!, req.DisciplineId, req.NewEducationForm, rooms);
            return Results.Ok(ToResponse(result));
        }).WithName("WhatIfModeChange").WithTags("WhatIf");
    }

    private static async Task<(WhatIfService? svc, ObjectiveWeights? weights, Timetable? timetable)>
        BuildContext(Guid runId, AnthroDispatchDbContext db, CancellationToken ct)
    {
        var run = await db.OptimizationRuns.FindAsync([runId], cancellationToken: ct);
        if (run is null) return (null, null, null);

        var groups = await db.Groups.ToListAsync(ct);
        var instructors = await db.Instructors.ToListAsync(ct);
        var disciplines = await db.Disciplines.ToListAsync(ct);
        var rooms = await db.Rooms.ToListAsync(ct);
        var assignments = await db.Assignments.ToListAsync(ct);
        var compatibilities = await db.CognitiveCompatibilities.ToListAsync(ct);

        var objFn = new ObjectiveFunctionService(groups, instructors, disciplines, rooms, assignments, compatibilities);
        var repair = new RepairService(rooms, instructors);
        var svc = new WhatIfService(objFn, repair);
        var weights = new ObjectiveWeights { Tech = 0.25, Circ = 0.25, Psych = 0.25, Cogn = 0.25 };

        var timetable = new Timetable
        {
            Metrics = new TimetableMetrics
            {
                FTech = run.FTech, FCirc = run.FCirc, FPsych = run.FPsych, FCogn = run.FCogn,
                F = run.BestFitness, Conflicts = run.Conflicts
            }
        };
        try
        {
            var items = JsonSerializer.Deserialize<List<ScheduledClassDto>>(run.TimetableJson);
            if (items != null)
                foreach (var item in items)
                    timetable.Classes.Add(new ScheduledClass
                    {
                        Id = item.Id, AssignmentId = item.AssignmentId, GroupId = item.GroupId,
                        InstructorId = item.InstructorId, DisciplineId = item.DisciplineId, RoomId = item.RoomId,
                        Slot = new TimeSlot(item.Day, item.Period)
                    });
        }
        catch
        {
            // ignored
        }

        return (svc, weights, timetable);
    }

    private static object ToResponse(WhatIfResult r)
    {
        // Reuse the canonical Risk(x) formula (RiskModelService, §2.4) instead
        // of re-deriving it here — an earlier inline copy diverged from it
        // (used 1-FCogn instead of C_interf and dropped the Rchange term).
        var fStable = RiskModelService.FStable(r.Candidate, r.Original);
        var riskBefore = RiskModelService.Calculate(r.OriginalMetrics);
        var riskAfter = RiskModelService.Calculate(r.CandidateMetrics, fStable);

        return new
        {
            scenarioId = r.ScenarioId,
            deltaF = r.DeltaF,
            fDynamic = r.FDynamic,
            changedClasses = r.ChangedClasses,
            riskBefore,
            riskAfter,
            explanation = r.Explanation
        };
    }
}