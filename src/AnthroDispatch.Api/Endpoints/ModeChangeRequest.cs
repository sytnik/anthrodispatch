using AnthroDispatch.Domain.Enums;

namespace AnthroDispatch.Api.Endpoints;

public sealed record ModeChangeRequest(Guid RunId, Guid DisciplineId, EducationForm NewEducationForm);