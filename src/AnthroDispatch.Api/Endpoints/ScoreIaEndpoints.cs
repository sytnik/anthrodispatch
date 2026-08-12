using System.Text.Json;
using AnthroDispatch.Infrastructure.Data;

namespace AnthroDispatch.Api.Endpoints;

public static class ScoreIaEndpoints
{
    public static void MapScoreIaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/optimization/{runId:guid}/candidates",
            async (Guid runId, AnthroDispatchDbContext db, CancellationToken ct) =>
            {
                var run = await db.OptimizationRuns.FindAsync([runId], cancellationToken: ct);
                if (run is null) return Results.NotFound();

                var candidates = JsonSerializer.Deserialize<List<RankedCandidateDto>>(run.CandidatesJson) ?? [];
                return Results.Ok(new { runId, candidates });
            }).WithName("GetRankedCandidates").WithTags("ScoreIa");
    }
}
