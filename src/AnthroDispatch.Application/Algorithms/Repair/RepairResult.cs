using AnthroDispatch.Domain.Entities;

namespace AnthroDispatch.Application.Algorithms.Repair;

public sealed record RepairResult(Timetable Timetable, int FixedConflicts, int RemainingConflicts);