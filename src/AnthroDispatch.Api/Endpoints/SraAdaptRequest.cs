namespace AnthroDispatch.Api.Endpoints;

public sealed record SraAdaptRequest(
    Guid DatasetId,
    Guid RunId,
    int Participants = 120,
    int Seed = 42,
    WeightsDto? OldWeights = null);