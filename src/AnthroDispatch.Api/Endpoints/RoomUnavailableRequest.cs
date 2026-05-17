namespace AnthroDispatch.Api.Endpoints;

public sealed record RoomUnavailableRequest(Guid RunId, Guid RoomId, int Day);