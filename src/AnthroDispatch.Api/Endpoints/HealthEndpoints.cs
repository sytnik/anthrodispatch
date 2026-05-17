namespace AnthroDispatch.Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }))
            .WithName("Health")
            .WithTags("Health");
    }
}