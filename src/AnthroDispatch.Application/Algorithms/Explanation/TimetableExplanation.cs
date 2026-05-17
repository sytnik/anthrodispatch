namespace AnthroDispatch.Application.Algorithms.Explanation;

public sealed record TimetableExplanation(
    Guid TimetableId,
    List<string> Strengths,
    List<string> Weaknesses,
    List<string> Recommendations,
    Dictionary<string, double> ComponentScores);