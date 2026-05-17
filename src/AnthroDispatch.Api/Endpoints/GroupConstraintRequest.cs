using AnthroDispatch.Domain.Enums;

namespace AnthroDispatch.Api.Endpoints;

public sealed record GroupConstraintRequest(
    Guid RunId,
    Guid GroupId,
    ConstraintType ConstraintType,
    int? Day = null,
    int? Period = null);