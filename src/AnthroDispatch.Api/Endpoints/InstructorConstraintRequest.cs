using AnthroDispatch.Domain.Enums;

namespace AnthroDispatch.Api.Endpoints;

public sealed record InstructorConstraintRequest(
    Guid RunId,
    Guid InstructorId,
    ConstraintType ConstraintType,
    int? Day = null,
    int? Period = null);