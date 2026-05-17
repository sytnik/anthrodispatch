namespace AnthroDispatch.Domain.Entities;

public sealed class OptimizationRun
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid DatasetId { get; init; }

    /// <summary>Set when the run was triggered via /api/optimization/run with dispatchProblemId.</summary>
    public Guid? DispatchProblemId { get; init; }

    public string Algorithm { get; init; } = "";
    public double BestFitness { get; set; }
    public double FTech { get; set; }
    public double FCirc { get; set; }
    public double FPsych { get; set; }
    public double FCogn { get; set; }
    public int Conflicts { get; set; }
    public int Generations { get; set; }
    public double TimeToF075Seconds { get; set; }
    public string TimetableJson { get; set; } = "{}";
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}