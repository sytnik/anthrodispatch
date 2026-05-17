using AnthroDispatch.Application.Algorithms.Objective;
using AnthroDispatch.Application.Algorithms.Repair;
using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.Enums;
using AnthroDispatch.Domain.Metrics;

namespace AnthroDispatch.Application.Algorithms.Awm;

/// <summary>
/// Anthropocentric Weighted Mutation
/// Slot quality: q(d,s,g) = w₂·a(d,s,g) + w₃·(1−l(d,s,g)) + w₄·c(d,s,g)
/// Selection probability: softmax(-β·q) — biases toward low-quality slots.
/// </summary>
public sealed class AwmMutation(
    List<AcademicGroup> groups,
    List<Instructor> instructors,
    List<CognitiveCompatibility> compatibilities,
    RepairService repair,
    double beta,
    Random rng,
    List<Discipline>? disciplines = null)
{
    private readonly List<Discipline> _disciplines = disciplines ?? [];

    public void Mutate(Timetable t, ObjectiveWeights weights)
    {
        if (t.Classes.Count < 2) return;

        var groupDict = groups.ToDictionary(g => g.Id);
        var instrDict = instructors.ToDictionary(i => i.Id);
        var discDict = _disciplines.ToDictionary(d => d.Id);
        var compatDict = compatibilities.ToDictionary(c => (c.FromDisciplineId, c.ToDisciplineId), c => c.Score);

        // q(d,s,g) = w₂·a(circadian activity) + w₃·(1−loadNorm) + w₄·cogn
        var qualities = t.Classes.Select(sc =>
        {
            // w₂ · circadian activity
            var a = groupDict.TryGetValue(sc.GroupId, out var g)
                ? CircadianActivityCalculator.Calculate(g.Chronotype, sc.Slot.Period)
                : 0.5;

            // w₃ · (1 − normalised load level)
            var loadNorm = 0.5;
            if (discDict.TryGetValue(sc.DisciplineId, out var disc))
                loadNorm = disc.LoadLevel switch
                {
                    CognitiveLoadLevel.Low => 0.0,
                    CognitiveLoadLevel.Medium => 0.5,
                    CognitiveLoadLevel.High => 1.0,
                    _ => 0.5
                };

            // w₄ · average cognitive compatibility of this class with adjacent classes
            var avgCompat = 0.5;
            var sameGroupDay = t.Classes
                .Where(c => c != sc && c.GroupId == sc.GroupId && c.Slot.Day == sc.Slot.Day)
                .ToList();
            if (sameGroupDay.Count > 0)
            {
                double sumCompat = 0;
                var cnt = 0;
                foreach (var other in sameGroupDay)
                {
                    if (compatDict.TryGetValue((sc.DisciplineId, other.DisciplineId), out var s))
                    {
                        sumCompat += (s + 1.0) / 2.0;
                        cnt++;
                    }
                }

                if (cnt > 0) avgCompat = sumCompat / cnt;
            }

            var q = weights.Circ * a + weights.Psych * (1 - loadNorm) + weights.Cogn * avgCompat;
            return (sc, q);
        }).ToList();

        // Softmax(-β·q): lower quality → higher selection probability
        var scores = qualities.Select(x => Math.Exp(-beta * x.q)).ToArray();
        var total = scores.Sum();
        if (total <= 0) return;

        var i1 = SampleCategorical(scores, total);
        var i2 = SampleCategorical(scores, total);
        if (i1 == i2) return;

        // Swap slots
        (t.Classes[i1].Slot, t.Classes[i2].Slot) = (t.Classes[i2].Slot, t.Classes[i1].Slot);

        repair.Repair(t);
    }

    private int SampleCategorical(double[] weights, double total)
    {
        var r = rng.NextDouble() * total;
        double cumulative = 0;
        for (var i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (r <= cumulative) return i;
        }

        return weights.Length - 1;
    }
}