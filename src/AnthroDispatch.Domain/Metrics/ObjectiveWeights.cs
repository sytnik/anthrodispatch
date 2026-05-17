namespace AnthroDispatch.Domain.Metrics;

public sealed class ObjectiveWeights
{
    public double Tech { get; set; } = 0.25;
    public double Circ { get; set; } = 0.25;
    public double Psych { get; set; } = 0.25;
    public double Cogn { get; set; } = 0.25;

    public static ObjectiveWeights Default => new();

    public static ObjectiveWeights ExpertReference => new()
    {
        Tech = 0.15,
        Circ = 0.30,
        Psych = 0.35,
        Cogn = 0.20
    };

    public ObjectiveWeights Clone() => new() { Tech = Tech, Circ = Circ, Psych = Psych, Cogn = Cogn };

    public void Validate()
    {
        const double min = 0.05;
        if (Tech < min || Circ < min || Psych < min || Cogn < min)
            throw new InvalidOperationException("All weights must be >= 0.05");
        var sum = Tech + Circ + Psych + Cogn;
        if (Math.Abs(sum - 1.0) > 1e-9)
            throw new InvalidOperationException($"Weights must sum to 1, but sum is {sum}");
    }
}