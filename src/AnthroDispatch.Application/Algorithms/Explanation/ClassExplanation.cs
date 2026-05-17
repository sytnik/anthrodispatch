namespace AnthroDispatch.Application.Algorithms.Explanation;

public sealed record ClassExplanation(
    Guid ScheduledClassId,
    List<string> Reasons,
    Dictionary<string, double> ComponentScores,
    List<string> TradeOffs);