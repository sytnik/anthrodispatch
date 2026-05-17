using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.Metrics;

namespace AnthroDispatch.Application.Algorithms.WhatIf;

public sealed record WhatIfResult(
    Guid ScenarioId,
    Timetable Original,
    Timetable Candidate,
    TimetableMetrics OriginalMetrics,
    TimetableMetrics CandidateMetrics,
    double DeltaF,
    double FDynamic,
    int ChangedClasses,
    List<string> Explanation);