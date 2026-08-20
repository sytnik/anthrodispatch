using AnthroDispatch.Application.Abstractions;
using AnthroDispatch.Application.Algorithms.Genetic;
using AnthroDispatch.Application.Algorithms.Objective;
using AnthroDispatch.Application.Algorithms.Repair;
using AnthroDispatch.Application.Algorithms.Sra;
using AnthroDispatch.Application.DataPreparation;
using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.Enums;
using AnthroDispatch.Domain.Metrics;
using AnthroDispatch.Infrastructure.MockData;

var command = args.Length > 0 ? args[0] : "help";
var seed = GetArg(args, "--seed", 42);
var pop = GetArg(args, "--pop", 50);
var gen = GetArg(args, "--gen", 100);
var runs = GetArg(args, "--runs", 5);
var algo = GetArgStr(args, "--algorithm", "AMD");
// Dataset-scale overrides: defaults keep prior fast-dev behavior (8/12/14/8) unchanged for
// existing callers; pass --groups 18 --instructors 60 --disciplines 42 --rooms 25 to reproduce
// the "Algorithmic verification" dataset scale reported in the article (§4.4).
var dsGroups = GetArg(args, "--groups", 8);
var dsInstructors = GetArg(args, "--instructors", 12);
var dsDisciplines = GetArg(args, "--disciplines", 14);
var dsRooms = GetArg(args, "--rooms", 8);
var algoFilter = GetArgStr(args, "--algorithms", "");

switch (command.ToLowerInvariant())
{
    case "generate":
        await RunGenerate(seed);
        break;
    case "optimize":
        await RunOptimize(seed, pop, gen, algo, dsGroups, dsInstructors, dsDisciplines, dsRooms);
        break;
    case "ablation":
        await RunAblation(seed, pop, gen, runs, dsGroups, dsInstructors, dsDisciplines, dsRooms, algoFilter);
        break;
    case "sra-sensitivity":
        RunSraSensitivity(seed);
        break;
    default:
        PrintHelp();
        break;
}

// ── Dataset pipeline helpers ─────────────────────────────────────────────────

/// <summary>
/// Generates an AnthroDispatchDataset + bridges AtomicSchedulingUnits →
/// legacy TeachingAssignments so existing GA services work unchanged.
/// </summary>
static async Task<(List<AcademicGroup> groups,
        List<Instructor> instructors,
        List<Discipline> disciplines,
        List<Room> rooms,
        List<TeachingAssignment> assignments,
        List<CognitiveCompatibility> compat)>
    BuildGaInputs(int seed, int dsGroups = 8, int dsInstructors = 12, int dsDisciplines = 14, int dsRooms = 8)
{
    var generator = new AnthroDispatchMockDataGenerator();
    var opts = new AnthroDispatchGenerationOptions(
        Seed: seed, AcademicYears: 2, Departments: 4, Degrees: 2,
        EducationalPrograms: 3, CurriculumPlans: 3, Terms: 4,
        Groups: dsGroups, StudentsApprox: dsGroups * 20, Instructors: dsInstructors,
        Disciplines: dsDisciplines, Rooms: dsRooms);
    var dataset = await generator.GenerateAsync(opts);

    var builder = new DispatchInputBuilder();
    var problem = builder.Build(dataset);

    // Bridge AtomicSchedulingUnit → TeachingAssignment (same as /api/optimization/run)
    var assignments = problem.AtomicUnits.Select(u => new TeachingAssignment
    {
        Id = u.Id,
        GroupId = u.GroupIds.FirstOrDefault(),
        InstructorId = u.InstructorIds.FirstOrDefault(),
        DisciplineId = u.DisciplineId,
        ClassType = u.LessonType switch
        {
            LessonType.Laboratory => ClassType.Laboratory,
            LessonType.Practice => ClassType.Practice,
            LessonType.Seminar => ClassType.Seminar,
            LessonType.Online => ClassType.Online,
            _ => ClassType.Lecture
        },
        RequiredPeriods = Math.Clamp(u.RequiredPeriods, 1, 6)
    }).Where(a => a.GroupId != Guid.Empty && a.DisciplineId != Guid.Empty).ToList();

    return (problem.Groups.ToList(), problem.Instructors.ToList(),
        problem.Disciplines.ToList(), problem.Rooms.ToList(),
        assignments, problem.CognitiveCompatibilityMatrix.ToList());
}

// ── Commands ─────────────────────────────────────────────────────────────────

static async Task RunGenerate(int seed)
{
    var generator = new AnthroDispatchMockDataGenerator();
    var opts = new AnthroDispatchGenerationOptions(
        Seed: seed, AcademicYears: 2, Departments: 6, Degrees: 2,
        EducationalPrograms: 4, CurriculumPlans: 4, Terms: 8,
        Groups: 18, StudentsApprox: 400, Instructors: 60,
        Disciplines: 20, Rooms: 25);
    var dataset = await generator.GenerateAsync(opts);

    Console.WriteLine($"AnthroDispatch dataset generated  (seed={seed}):");
    Console.WriteLine($"  Academic years:        {dataset.AcademicYears.Count}");
    Console.WriteLine($"  Degrees:               {dataset.Degrees.Count}");
    Console.WriteLine($"  Departments:           {dataset.Departments.Count}");
    Console.WriteLine($"  Educational programs:  {dataset.EducationalPrograms.Count}");
    Console.WriteLine($"  Curriculum plans:      {dataset.CurriculumPlans.Count}");
    Console.WriteLine($"  Curriculum plan items: {dataset.CurriculumPlanItems.Count}");
    Console.WriteLine($"  Calendar terms:        {dataset.AcademicCalendarTerms.Count}");
    Console.WriteLine($"  Learning assignments:  {dataset.LearningAssignments.Count}");
    Console.WriteLine($"  Groups:                {dataset.Groups.Count}");
    Console.WriteLine($"  Instructors:           {dataset.Instructors.Count}");
    Console.WriteLine($"  Disciplines:           {dataset.Disciplines.Count}");
    Console.WriteLine($"  Rooms:                 {dataset.Rooms.Count}");
    Console.WriteLine($"  Health limitations:    {dataset.HealthLimitations.Count}");
    Console.WriteLine($"  Instructor constraints:{dataset.InstructorConstraints.Count}");
    Console.WriteLine($"  Group constraints:     {dataset.GroupConstraints.Count}");
    Console.WriteLine($"  Prerequisite edges:    {dataset.CurriculumPlanItemEdges.Count}");
    Console.WriteLine($"  Compatibility scores:  {dataset.CognitiveCompatibilities.Count}");

    var builder = new DispatchInputBuilder();
    var problem = builder.Build(dataset);
    Console.WriteLine("\nDispatchProblem built:");
    Console.WriteLine($"  Atomic units:          {problem.AtomicUnits.Count}");
    Console.WriteLine($"  Horizon: days={problem.Horizon.Days}, periods/day={problem.Horizon.PeriodsPerDay}");
}

static async Task RunOptimize(int seed, int pop, int gen, string algo,
    int dsGroups = 8, int dsInstructors = 12, int dsDisciplines = 14, int dsRooms = 8)
{
    var (groups, instructors, disciplines, rooms, assignments, compat) =
        await BuildGaInputs(seed, dsGroups, dsInstructors, dsDisciplines, dsRooms);

    var weights = ObjectiveWeights.Default;
    var objFn = new ObjectiveFunctionService(groups, instructors, disciplines, rooms, assignments, compat);
    var repair = new RepairService(rooms, instructors, groups, assignments);
    var opts = new GaOptions { PopulationSize = pop, MaxGenerations = gen, Seed = seed };

    var result = algo.ToUpperInvariant() switch
    {
        "AMD" => new AmdService(groups, instructors, disciplines, rooms, assignments, compat, objFn, repair, opts)
            .Run(weights),
        "CPCGA" => new CpcGaService(groups, instructors, disciplines, rooms, assignments, compat, objFn, repair, opts)
            .Run(weights),
        "AWMGA" => new AwmGaService(groups, instructors, disciplines, rooms, assignments, compat, objFn, repair, opts)
            .Run(weights),
        "NSGA2" or "NSGAII" => new NsgaIIService(groups, instructors, disciplines, rooms, assignments, compat, objFn,
            repair, opts).Run(weights),
        _ => new BaselineGaService(groups, instructors, disciplines, rooms, assignments, compat, objFn, repair, opts)
            .Run(weights)
    };

    Console.WriteLine($"\nAlgorithm: {algo}  (seed={seed}, pop={pop}, gen={gen})");
    Console.WriteLine($"  F      = {result.BestMetrics.F:F4}");
    Console.WriteLine($"  FTech  = {result.BestMetrics.FTech:F4}");
    Console.WriteLine($"  FCirc  = {result.BestMetrics.FCirc:F4}");
    Console.WriteLine($"  FPsych = {result.BestMetrics.FPsych:F4}");
    Console.WriteLine($"  FCogn  = {result.BestMetrics.FCogn:F4}");
    Console.WriteLine($"  Conflicts   = {result.BestMetrics.Conflicts}");
    Console.WriteLine($"  Generations = {result.GenerationsRun}");
    Console.WriteLine($"  t(F>0.75)   = {result.TimeToF075Seconds / 60.0:F1} min");
}

static async Task RunAblation(int seed, int pop, int gen, int runs,
    int dsGroups = 8, int dsInstructors = 12, int dsDisciplines = 14, int dsRooms = 8, string algoFilter = "")
{
    var (groups, instructors, disciplines, rooms, assignments, compat) =
        await BuildGaInputs(seed, dsGroups, dsInstructors, dsDisciplines, dsRooms);

    var weights = ObjectiveWeights.Default;
    var allAlgos = new[] { "BaselineGA", "CpcGA", "AwmGA", "AMD", "NSGA2" };
    var algos = string.IsNullOrWhiteSpace(algoFilter)
        ? allAlgos
        : algoFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    Console.WriteLine(
        $"\n{"Algorithm",-14} {"F100",-8} {"F500",-8} {"t(F>0.65),min",-16} {"t(F>0.75),min",-16} {"σ",-8}");
    Console.WriteLine(new string('-', 70));

    foreach (var algName in algos)
    {
        // Runs are independent (each builds its own ObjFn/Repair/GaOptions/RNG from a distinct
        // seed) and only read the shared groups/instructors/disciplines/rooms/assignments/compat
        // input lists, never mutate them — safe to parallelize across all available cores instead
        // of running 30 independent trials sequentially.
        var fitnesses = new double[runs];
        var f100Arr = new double[runs];
        var tToF065 = new double[runs];
        var tToF075 = new double[runs];
        var fTechArr = new double[runs];
        var fCircArr = new double[runs];
        var fPsychArr = new double[runs];
        var fCognArr = new double[runs];

        Parallel.For(0, runs, r =>
        {
            var objFn = new ObjectiveFunctionService(groups, instructors, disciplines, rooms, assignments, compat);
            var repair = new RepairService(rooms, instructors, groups, assignments);
            var opts = new GaOptions { PopulationSize = pop, MaxGenerations = gen, Seed = seed + r };

            var optResult = algName switch
            {
                "AMD" => new AmdService(groups, instructors, disciplines, rooms, assignments, compat, objFn, repair,
                    opts).Run(weights),
                "CpcGA" => new CpcGaService(groups, instructors, disciplines, rooms, assignments, compat, objFn, repair,
                    opts).Run(weights),
                "AwmGA" => new AwmGaService(groups, instructors, disciplines, rooms, assignments, compat, objFn, repair,
                    opts).Run(weights),
                "NSGA2" => new NsgaIIService(groups, instructors, disciplines, rooms, assignments, compat, objFn,
                    repair, opts).Run(weights),
                _ => new BaselineGaService(groups, instructors, disciplines, rooms, assignments, compat, objFn, repair,
                    opts).Run(weights)
            };

            fitnesses[r] = optResult.BestMetrics.F;
            f100Arr[r] = optResult.FitnessHistory.Count >= 100
                ? optResult.FitnessHistory[99]
                : optResult.BestMetrics.F;
            tToF065[r] = optResult.TimeToF065Seconds / 60.0;
            tToF075[r] = optResult.TimeToF075Seconds / 60.0;
            fTechArr[r] = optResult.BestMetrics.FTech;
            fCircArr[r] = optResult.BestMetrics.FCirc;
            fPsychArr[r] = optResult.BestMetrics.FPsych;
            fCognArr[r] = optResult.BestMetrics.FCogn;

            Console.WriteLine($"  [{algName}] run {r + 1}/{runs} done, F={optResult.BestMetrics.F:F4}");
        });

        static double StdOf(double[] xs)
        {
            if (xs.Length <= 1) return 0;
            var m = xs.Average();
            return Math.Sqrt(xs.Average(v => (v - m) * (v - m)));
        }

        var mean = fitnesses.Average();
        var std = StdOf(fitnesses);
        var f100 = f100Arr.Average();
        var f100Std = StdOf(f100Arr);
        var t065 = tToF065.Where(x => x >= 0).DefaultIfEmpty(0).Average();
        var t065Std = StdOf(tToF065.Where(x => x >= 0).ToArray());
        var tAvg = tToF075.Where(x => x >= 0).DefaultIfEmpty(0).Average();
        var tAvgStd = StdOf(tToF075.Where(x => x >= 0).ToArray());
        Console.WriteLine(
            $"  [{algName}] component means: FTech={fTechArr.Average():F4} FCirc={fCircArr.Average():F4} " +
            $"FPsych={fPsychArr.Average():F4} FCogn={fCognArr.Average():F4}");
        Console.WriteLine(
            $"  [{algName}] F100={f100:F4}±{f100Std:F4} F500={mean:F4}±{std:F4} " +
            $"t065={t065:F1}±{t065Std:F1} t075={tAvg:F1}±{tAvgStd:F1}");

        Console.WriteLine($"{algName,-14} {f100,-8:F3} {mean,-8:F3} {t065,-16:F1} {tAvg,-16:F1} {std,-8:F3}");
    }
}

/// <summary>
/// SRA feedback-noise/sparsity sensitivity analysis (article revision, Reviewer 1 request):
/// how does the SRA weight-adaptation regression degrade when post-semester feedback is (a)
/// missing (fewer respondents than expected) or (b) highly inconsistent (noisier/less reliable
/// ratings) relative to the well-conditioned baseline? Reports distance-to-reference and
/// correlation-to-reference (both already computed by SraService against w* = (0.15,0.30,0.35,0.20),
/// the same reference vector used in §3.4/§4.7 of the article) across repeated trials per
/// condition, mean ± SD.
/// </summary>
static void RunSraSensitivity(int seed)
{
    const int baselineN = 80; // above the N=50 ridge threshold, so the baseline uses plain OLS
    const int reps = 50;
    var svc = new SraService();
    var oldWeights = ObjectiveWeights.Default;

    List<TimetableMetrics> BuildSamples(int count, int sampleSeed)
    {
        var rng = new Random(sampleSeed);
        return Enumerable.Range(0, count)
            .Select(_ => new TimetableMetrics
            {
                FTech = rng.NextDouble(),
                FCirc = rng.NextDouble(),
                FPsych = rng.NextDouble(),
                FCogn = rng.NextDouble()
            })
            .ToList();
    }

    (double distMean, double distStd, double corrMean, double corrStd) RunCondition(
        int n, double noiseStdDev)
    {
        var dists = new List<double>();
        var corrs = new List<double>();
        for (var r = 0; r < reps; r++)
        {
            var trialSeed = seed + r;
            var samples = BuildSamples(n, trialSeed);
            var result = svc.Adapt(samples, oldWeights, trialSeed, noiseStdDev);
            dists.Add(result.DistanceToReference);
            corrs.Add(result.CorrelationToReference);
        }

        var dMean = dists.Average();
        var dStd = Math.Sqrt(dists.Average(x => (x - dMean) * (x - dMean)));
        var cMean = corrs.Average();
        var cStd = Math.Sqrt(corrs.Average(x => (x - cMean) * (x - cMean)));
        return (dMean, dStd, cMean, cStd);
    }

    Console.WriteLine($"\nSRA feedback sensitivity analysis (baseline N={baselineN}, {reps} reps/condition, seed={seed})");
    Console.WriteLine(
        $"{"Condition",-28} {"N",-6} {"noise σ",-9} {"dist(mean±SD)",-18} {"corr(mean±SD)",-18}");
    Console.WriteLine(new string('-', 82));

    var missingConditions = new (string Label, double Fraction)[]
    {
        ("Baseline (0% missing)", 0.0),
        ("10% missing", 0.10),
        ("20% missing", 0.20)
    };
    foreach (var (label, frac) in missingConditions)
    {
        var n = (int)Math.Round(baselineN * (1 - frac));
        var (dMean, dStd, cMean, cStd) = RunCondition(n, 0.25);
        Console.WriteLine($"{label,-28} {n,-6} {"0.25",-9} {$"{dMean:F4} ± {dStd:F4}",-18} {$"{cMean:F4} ± {cStd:F4}",-18}");
    }

    var noiseConditions = new (string Label, double NoiseStdDev)[]
    {
        ("Baseline (σ=0.25)", 0.25),
        ("Moderate inconsistency (σ=0.375)", 0.375),
        ("Severe inconsistency (σ=0.50)", 0.50)
    };
    foreach (var (label, noiseStdDev) in noiseConditions)
    {
        var (dMean, dStd, cMean, cStd) = RunCondition(baselineN, noiseStdDev);
        Console.WriteLine(
            $"{label,-28} {baselineN,-6} {noiseStdDev,-9:F3} {$"{dMean:F4} ± {dStd:F4}",-18} {$"{cMean:F4} ± {cStd:F4}",-18}");
    }
}

static void PrintHelp()
{
    Console.WriteLine("AnthroDispatch Console Runner");
    Console.WriteLine("  generate  [--seed N]");
    Console.WriteLine("  optimize  [--seed N] [--pop N] [--gen N] [--algorithm BaselineGA|CpcGA|AwmGA|AMD|NSGA2]");
    Console.WriteLine("            [--groups N] [--instructors N] [--disciplines N] [--rooms N]");
    Console.WriteLine("  ablation  [--seed N] [--pop N] [--gen N] [--runs N]");
    Console.WriteLine("            [--groups N] [--instructors N] [--disciplines N] [--rooms N]");
    Console.WriteLine("  sra-sensitivity [--seed N]");
    Console.WriteLine("  Dataset-scale defaults are the small fast-dev set (8/12/14/8);");
    Console.WriteLine("  pass --groups 18 --instructors 60 --disciplines 42 --rooms 25 to match the");
    Console.WriteLine("  article's \"Algorithmic verification\" dataset (§4.4).");
}

static int GetArg(string[] args, string key, int def)
{
    var idx = Array.IndexOf(args, key);
    return idx >= 0 && idx + 1 < args.Length && int.TryParse(args[idx + 1], out var v) ? v : def;
}

static string GetArgStr(string[] args, string key, string def)
{
    var idx = Array.IndexOf(args, key);
    return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : def;
}