using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.Metrics;

namespace AnthroDispatch.Application.Algorithms.Genetic;

public sealed record OptimizationResult(
    Timetable BestTimetable,
    TimetableMetrics BestMetrics,
    List<double> FitnessHistory,
    int GenerationsRun,
    double TimeToF075Seconds,
    double TimeToF065Seconds = -1);