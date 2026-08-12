namespace AnthroDispatch.Application.Algorithms.Genetic;

public sealed record GaOptions
{
    public int PopulationSize { get; init; } = 200;
    public int MaxGenerations { get; init; } = 500;
    public int TournamentSize { get; init; } = 5;
    public double CrossoverProbability { get; init; } = 0.85;
    public double MutationProbability { get; init; } = 0.15;
    public double AwmBeta { get; init; } = 2.0;
    public double CpcGamma { get; init; } = 5.0;
    public int StagnationGenerations { get; init; } = 50;
    public double StagnationThreshold { get; init; } = 0.001;
    public double EliteFraction { get; init; } = 0.10;
    public int Seed { get; init; } = 42;

    /// <summary>
    /// Size of the top-m candidate set (X_cand = {x1,...,xm}, dissertation
    /// §2.4) retained from the final population for IA ranking (Score_IA).
    /// The dissertation does not specify m numerically; 5 is a modest
    /// implementation default and can be overridden per run.
    /// </summary>
    public int TopMCandidates { get; init; } = 5;

    public static GaOptions FastDev => new()
    {
        PopulationSize = 50,
        MaxGenerations = 100,
        StagnationGenerations = 20
    };
}