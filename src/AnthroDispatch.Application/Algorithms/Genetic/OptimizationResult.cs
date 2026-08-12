using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.Metrics;

namespace AnthroDispatch.Application.Algorithms.Genetic;

/// <param name="TopCandidates">
/// X_cand = {x1,...,xm}: top-m individuals from the final population,
/// sorted descending by F (dissertation §2.4). Null for callers that have
/// not been updated to populate it (defensive default; none of the four GA
/// services in this repo currently omit it).
/// </param>
public sealed record OptimizationResult(
    Timetable BestTimetable,
    TimetableMetrics BestMetrics,
    List<double> FitnessHistory,
    int GenerationsRun,
    double TimeToF075Seconds,
    double TimeToF065Seconds = -1,
    List<Timetable>? TopCandidates = null);