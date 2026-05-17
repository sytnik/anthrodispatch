namespace AnthroDispatch.Api.Endpoints;

public sealed record InstructorUnavailableRequest(Guid RunId, Guid InstructorId, int Day, int? Period = null);