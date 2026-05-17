namespace AnthroDispatch.Api.Endpoints;

public sealed record GroupUnavailableRequest(Guid RunId, Guid GroupId, int Day, int Period);