namespace AnthroDispatch.Api.Endpoints;

public sealed record WeightsChangedRequest(Guid RunId, WeightsDto OldWeights, WeightsDto NewWeights);