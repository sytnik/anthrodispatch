namespace AnthroDispatch.Api.Endpoints;

public sealed record AblationRequest(
    Guid DatasetId,
    int Seed = 42,
    int Runs = 5,
    int PopulationSize = 50,
    int MaxGenerations = 100);