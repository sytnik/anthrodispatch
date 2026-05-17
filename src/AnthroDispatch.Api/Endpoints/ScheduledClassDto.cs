namespace AnthroDispatch.Api.Endpoints;

/// <summary>Internal DTO used for JSON deserialization of stored timetable classes.</summary>
internal sealed record ScheduledClassDto(
    Guid Id,
    Guid AssignmentId,
    Guid GroupId,
    Guid InstructorId,
    Guid DisciplineId,
    Guid RoomId,
    int Day,
    int Period);