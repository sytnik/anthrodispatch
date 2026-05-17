namespace AnthroDispatch.Application.Abstractions;

public sealed record DatasetGenerationRequest(
    int Seed,
    int Groups,
    int StudentsApprox,
    int Instructors,
    int Disciplines,
    int Rooms);