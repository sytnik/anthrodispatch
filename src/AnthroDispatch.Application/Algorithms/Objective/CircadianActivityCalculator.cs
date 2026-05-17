using AnthroDispatch.Domain.Enums;

namespace AnthroDispatch.Application.Algorithms.Objective;

public static class CircadianActivityCalculator
{
    private const double SigmaSquared = 2.5;

    private static int GetPeak(ChronotypeCategory chronotype) => chronotype switch
    {
        ChronotypeCategory.DefiniteMorning => 2,
        ChronotypeCategory.ModerateMorning => 3,
        ChronotypeCategory.Intermediate => 4,
        ChronotypeCategory.ModerateEvening => 6,
        ChronotypeCategory.DefiniteEvening => 7,
        _ => 4
    };

    public static double Calculate(ChronotypeCategory chronotype, int period)
    {
        var peak = GetPeak(chronotype);
        var diff = period - peak;
        return Math.Exp(-(diff * diff) / (2.0 * SigmaSquared));
    }

    // Age-aware modifiers
    /// <summary>Age modifier: older instructors (>45) have slightly reduced circadian amplitude.</summary>
    public static double AgeModifier(int age)
        => Math.Clamp(1.0 - 0.10 * ((age - 45.0) / 35.0), 0.85, 1.05);

    /// <summary>Age modifier accepting a double (for group average age).</summary>
    private static double AgeModifier(double averageAge)
        => AgeModifier((int)Math.Round(averageAge));

    /// <summary>Age-aware circadian activity for individuals.</summary>
    public static double Calculate(ChronotypeCategory chronotype, int period, int age)
        => Calculate(chronotype, period) * AgeModifier(age);

    /// <summary>Age-aware circadian activity for groups (uses average age).</summary>
    public static double Calculate(ChronotypeCategory chronotype, int period, double averageAge)
        => Calculate(chronotype, period) * AgeModifier(averageAge);
}