using System.Text.Json;
using AnthroDispatch.Application.Algorithms.Explanation;
using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.ValueObjects;
using AnthroDispatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AnthroDispatch.Api.Endpoints;

public static class ExplanationEndpoints
{
    public static void MapExplanationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/optimization/{runId:guid}/explanation",
            async (Guid runId, AnthroDispatchDbContext db, CancellationToken ct) =>
            {
                var run = await db.OptimizationRuns.FindAsync([runId], cancellationToken: ct);
                if (run is null) return Results.NotFound();

                var groups = await db.Groups.ToListAsync(ct);
                var instructors = await db.Instructors.ToListAsync(ct);
                var disciplines = await db.Disciplines.ToListAsync(ct);
                var compatibilities = await db.CognitiveCompatibilities.ToListAsync(ct);

                var timetable = DeserializeTimetable(run.TimetableJson);
                var svc = new ExplanationService(groups, instructors, disciplines, compatibilities);
                var explanation = svc.ExplainTimetable(timetable);
                return Results.Ok(explanation);
            }).WithName("ExplainTimetable").WithTags("Explanation");

        app.MapGet("/api/optimization/{runId:guid}/classes/{classId:guid}/explanation", async (
            Guid runId, Guid classId, AnthroDispatchDbContext db, CancellationToken ct) =>
        {
            var run = await db.OptimizationRuns.FindAsync([runId], cancellationToken: ct);
            if (run is null) return Results.NotFound();

            var groups = await db.Groups.ToListAsync(ct);
            var instructors = await db.Instructors.ToListAsync(ct);
            var disciplines = await db.Disciplines.ToListAsync(ct);
            var compatibilities = await db.CognitiveCompatibilities.ToListAsync(ct);

            var timetable = DeserializeTimetable(run.TimetableJson);
            var svc = new ExplanationService(groups, instructors, disciplines, compatibilities);
            var explanation = svc.ExplainClass(timetable, classId);
            return Results.Ok(explanation);
        }).WithName("ExplainClass").WithTags("Explanation");
    }

    private static Timetable DeserializeTimetable(string json)
    {
        var timetable = new Timetable();
        try
        {
            var items = JsonSerializer.Deserialize<List<ScheduledClassDto>>(json);
            if (items != null)
            {
                foreach (var item in items)
                {
                    timetable.Classes.Add(new ScheduledClass
                    {
                        Id = item.Id,
                        AssignmentId = item.AssignmentId,
                        GroupId = item.GroupId,
                        InstructorId = item.InstructorId,
                        DisciplineId = item.DisciplineId,
                        RoomId = item.RoomId,
                        Slot = new TimeSlot(item.Day, item.Period)
                    });
                }
            }
        }
        catch
        {
            // ignored
        }

        return timetable;
    }
}