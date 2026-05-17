using AnthroDispatch.Domain.Entities;

namespace AnthroDispatch.Application.Abstractions;

public sealed record DatasetGenerationResult(
    Guid DatasetId,
    List<AcademicGroup> Groups,
    List<Instructor> Instructors,
    List<Discipline> Disciplines,
    List<Room> Rooms,
    List<TeachingAssignment> Assignments,
    List<CognitiveCompatibility> Compatibilities);