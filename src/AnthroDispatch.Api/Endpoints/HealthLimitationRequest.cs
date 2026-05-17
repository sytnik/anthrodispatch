using AnthroDispatch.Domain.Enums;

namespace AnthroDispatch.Api.Endpoints;

public sealed record HealthLimitationRequest(Guid RunId, HealthLimitationType LimitationType);