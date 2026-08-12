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

    /// <summary>
    /// C_interf(x) (dissertation §2.2): (1/|pairs(x)|)·Σmax(-s_kl, 0) — the
    /// negative-only aggregate of the cognitive compatibility matrix S over
    /// adjacent same-day pairs, used by RiskModelService as Risk_cognitive
    /// (§2.4). Unlike FCogn, positive (synergistic) pairs contribute 0, not a
    /// reduction — interference is risk, absence of synergy is not.
    /// </summary>
    public double CInterf { get; set; }

    public static TimetableMetrics Zero => new() { FTech = 0, FCirc = 0, FPsych = 0, FCogn = 0, F = 0, CInterf = 0 };
}