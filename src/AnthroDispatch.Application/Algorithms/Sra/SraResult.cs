using AnthroDispatch.Domain.Metrics;

namespace AnthroDispatch.Application.Algorithms.Sra;

public sealed record SraResult(
    ObjectiveWeights OldWeights,
    ObjectiveWeights NewWeights,
    double DistanceToReference,
    double CorrelationToReference);