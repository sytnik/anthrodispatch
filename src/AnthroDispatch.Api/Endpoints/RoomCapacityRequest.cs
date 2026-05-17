namespace AnthroDispatch.Api.Endpoints;

public sealed record RoomCapacityRequest(Guid RunId, Guid RoomId, int RequiredCapacity);