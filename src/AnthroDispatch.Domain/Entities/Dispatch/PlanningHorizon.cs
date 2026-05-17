namespace AnthroDispatch.Domain.Entities.Dispatch;

/// <summary>Defines the planning horizon: how many days and periods per day.</summary>
public sealed class PlanningHorizon
{
    public int Days { get; init; }
    public int PeriodsPerDay { get; init; }

    /// <summary>Default week: Day ∈ [1..6], Period ∈ [1..8].</summary>
    public static PlanningHorizon DefaultWeek() => new() { Days = 6, PeriodsPerDay = 8 };
}