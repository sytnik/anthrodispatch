namespace AnthroDispatch.Api.Endpoints;

/// <summary>
/// Accepts either dispatchProblemId or datasetId (legacy).
/// Both default to empty Guid; if dispatchProblemId is set the optimizer loads from cache.
/// </summary>
public sealed record RunOptimizationRequest(
    Guid DatasetId = default,
    Guid? DispatchProblemId = null,
    string Algorithm = "AMD",
    int Seed = 42,
    int PopulationSize = 50,
    int MaxGenerations = 100,
    WeightsDto? Weights = null);