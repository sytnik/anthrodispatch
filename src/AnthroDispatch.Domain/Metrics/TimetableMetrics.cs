namespace AnthroDispatch.Domain.Metrics;

public sealed class TimetableMetrics
{
    public double FTech { get; set; }
    public double FCirc { get; set; }
    public double FPsych { get; set; }
    public double FCogn { get; set; }
    public double F { get; set; }
    public int Conflicts { get; set; }
    public double Satisfaction { get; set; }

    public static TimetableMetrics Zero => new() { FTech = 0, FCirc = 0, FPsych = 0, FCogn = 0, F = 0 };
}