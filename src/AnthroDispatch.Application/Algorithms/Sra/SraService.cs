using AnthroDispatch.Domain.Metrics;
using MathNet.Numerics.LinearAlgebra;

namespace AnthroDispatch.Application.Algorithms.Sra;

public sealed class SraService
{
    private const double EmaAlpha = 0.4;
    private const double RidgeMu = 0.1;
    private const int RidgeSampleThreshold = 50;

    public SraResult Adapt(
        List<TimetableMetrics> samples,
        ObjectiveWeights oldWeights,
        int seed = 42)
    {
        var rng = new Random(seed);

        // Generate satisfaction scores from the expert reference model + noise
        var q = samples.Select(m =>
        {
            var signal = 1 + 4 * (0.15 * m.FTech + 0.30 * m.FCirc + 0.35 * m.FPsych + 0.20 * m.FCogn);
            var noise = SampleNormal(rng, 0, 0.25);
            return Math.Clamp(signal + noise, 1.0, 5.0);
        }).ToArray();

        // Build design matrix X = [1 | Ftech Fcirc Fpsych Fcogn]
        var n = samples.Count;
        var matrix = Matrix<double>.Build.Dense(n, 5, (i, j) => j switch
        {
            0 => 1.0,
            1 => samples[i].FTech,
            2 => samples[i].FCirc,
            3 => samples[i].FPsych,
            4 => samples[i].FCogn,
            _ => 0
        });
        var vector = Vector<double>.Build.DenseOfArray(q);

        // N >= 50: plain OLS, beta = (X'X)^-1 X'y
        // N <  50: ridge regression, beta = (X'X + mu*I)^-1 X'y (mu=0.1), to
        // stabilise the estimate on small samples. The intercept (column 0) is
        // not regularised, per standard ridge practice (only the four objective
        // weight coefficients are shrunk).
        var transpose = matrix.Transpose();
        Vector<double> beta;
        try
        {
            var gram = transpose * matrix;
            if (n < RidgeSampleThreshold)
            {
                var ridge = Matrix<double>.Build.DenseDiagonal(5, 5, i => i == 0 ? 0.0 : RidgeMu);
                gram += ridge;
            }
            beta = gram.Inverse() * (transpose * vector);
        }
        catch
        {
            // Fallback: use old weights unchanged
            return new SraResult(oldWeights, oldWeights.Clone(), 0, 0);
        }

        // Extract weights from β[1..4], clamp negative to 0
        var rawCoeffs = new[]
            { Math.Max(beta[1], 0), Math.Max(beta[2], 0), Math.Max(beta[3], 0), Math.Max(beta[4], 0) };
        var sum = rawCoeffs.Sum();
        if (sum <= 0) return new SraResult(oldWeights, oldWeights.Clone(), 0, 0);

        var wTilde = rawCoeffs.Select(c => c / sum).ToArray();

        // EMA update
        var wRaw = new[]
        {
            EmaAlpha * wTilde[0] + (1 - EmaAlpha) * oldWeights.Tech,
            EmaAlpha * wTilde[1] + (1 - EmaAlpha) * oldWeights.Circ,
            EmaAlpha * wTilde[2] + (1 - EmaAlpha) * oldWeights.Psych,
            EmaAlpha * wTilde[3] + (1 - EmaAlpha) * oldWeights.Cogn
        };

        // Simplex projection with lower bound 0.05
        var newWts = SimplexProject(wRaw);

        // Compute distance and correlation to reference
        var reference = new[] { 0.15, 0.30, 0.35, 0.20 };
        var dist = Math.Sqrt(new[]
        {
            newWts.Tech - reference[0], newWts.Circ - reference[1], newWts.Psych - reference[2],
            newWts.Cogn - reference[3]
        }.Sum(x => x * x));
        var corr = PearsonCorrelation([newWts.Tech, newWts.Circ, newWts.Psych, newWts.Cogn], reference);

        return new SraResult(oldWeights, newWts, dist, corr);
    }

    private static ObjectiveWeights SimplexProject(double[] w)
    {
        const double minW = 0.05;
        const int maxIter = 100;
        var v = (double[])w.Clone();

        for (var iter = 0; iter < maxIter; iter++)
        {
            // Clamp below
            for (var i = 0; i < v.Length; i++) v[i] = Math.Max(v[i], minW);
            var s = v.Sum();
            for (var i = 0; i < v.Length; i++) v[i] /= s;
            if (v.All(x => x >= minW - 1e-12) && Math.Abs(v.Sum() - 1.0) < 1e-12) break;
        }

        return new ObjectiveWeights { Tech = v[0], Circ = v[1], Psych = v[2], Cogn = v[3] };
    }

    private static double SampleNormal(Random rng, double mean, double stddev)
    {
        // Box-Muller
        var u1 = 1.0 - rng.NextDouble();
        var u2 = 1.0 - rng.NextDouble();
        return mean + stddev * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
    }

    private static double PearsonCorrelation(double[] a, double[] b)
    {
        double meanA = a.Average(), meanB = b.Average();
        var num = a.Zip(b, (x, y) => (x - meanA) * (y - meanB)).Sum();
        var denA = Math.Sqrt(a.Sum(x => (x - meanA) * (x - meanA)));
        var denB = Math.Sqrt(b.Sum(x => (x - meanB) * (x - meanB)));
        return denA * denB < 1e-12 ? 0 : num / (denA * denB);
    }
}