namespace AnthroDispatch.Domain.ValueObjects;

/// <summary>Day ∈ [1..6], Period ∈ [1..8]</summary>
public readonly record struct TimeSlot(int Day, int Period)
{
    public static readonly int MinDay = 1;
    public static readonly int MaxDay = 6;
    public static readonly int MinPeriod = 1;
    public static readonly int MaxPeriod = 8;

    public bool IsValid() => Day is >= 1 and <= 6 && Period is >= 1 and <= 8;
}