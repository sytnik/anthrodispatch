namespace AnthroDispatch.Api.Endpoints;

public sealed record DisciplineMovedRequest(Guid RunId, Guid DisciplineId, int TargetDay, int TargetPeriod);