using System.Text.Json;
using AnthroDispatch.Application.Algorithms.Conformance;
using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.ValueObjects;
using AnthroDispatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AnthroDispatch.Api.Endpoints;

public static class ConformanceEndpoints
{
    public static void MapConformanceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/optimization/{runId:guid}/conformance",
            async (Guid runId, AnthroDispatchDbContext db, CancellationToken ct) =>
            {
                var run = await db.OptimizationRuns.FindAsync([runId], cancellationToken: ct);
                if (run is null) return Results.NotFound();

                var rooms = await db.Rooms.ToListAsync(ct);
                var groups = await db.Groups.ToListAsync(ct);
                var assignments = await db.Assignments.ToListAsync(ct);

                var timetable = DeserializeTimetable(run.TimetableJson);
                var svc = new ConformanceCheckingService(rooms, groups, assignments);
                var result = svc.CheckConformance(timetable);
                return Results.Ok(result);
            }).WithName("CheckConformance").WithTags("Conformance");
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
