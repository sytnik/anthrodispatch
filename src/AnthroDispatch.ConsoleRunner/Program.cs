using AnthroDispatch.Application.Abstractions;
using AnthroDispatch.Application.Algorithms.Genetic;
using AnthroDispatch.Application.Algorithms.Objective;
using AnthroDispatch.Application.Algorithms.Repair;
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

switch (command.ToLowerInvariant())
{
    case "generate":
        await RunGenerate(seed);
        break;
    case "optimize":
        await RunOptimize(seed, pop, gen, algo);
        break;
    case "ablation":
        await RunAblation(seed, pop, gen, runs);
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
    BuildGaInputs(int seed)
{
    var generator = new AnthroDispatchMockDataGenerator();
    var opts = new AnthroDispatchGenerationOptions(
        Seed: seed, AcademicYears: 2, Departments: 4, Degrees: 2,
        EducationalPrograms: 3, CurriculumPlans: 3, Terms: 4,
        Groups: 8, StudentsApprox: 160, Instructors: 12,
        Disciplines: 14, Rooms: 8);
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

static async Task RunOptimize(int seed, int pop, int gen, string algo)
{
    var (groups, instructors, disciplines, rooms, assignments, compat) = await BuildGaInputs(seed);

    var weights = ObjectiveWeights.Default;
    var objFn = new ObjectiveFunctionService(groups, instructors, disciplines, rooms, assignments, compat);
    var repair = new RepairService(rooms, instructors);
    var opts = new GaOptions { PopulationSize = pop, MaxGenerations = gen, Seed = seed };

    var result = algo.ToUpperInvariant() switch
    {
        "AMD" => new AmdService(groups, instructors, disciplines, rooms, assignments, compat, objFn, repair, opts)
            .Run(weights),
        "CPCGA" => new CpcGaService(groups, instructors, disciplines, rooms, assignments, compat, objFn, repair, opts)
            .Run(weights),
        "AWMGA" => new AwmGaService(groups, instructors, disciplines, rooms, assignments, compat, objFn, repair, opts)
            .Run(weights),
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

static async Task RunAblation(int seed, int pop, int gen, int runs)
{
    var (groups, instructors, disciplines, rooms, assignments, compat) = await BuildGaInputs(seed);

    var weights = ObjectiveWeights.Default;
    var algos = new[] { "BaselineGA", "CpcGA", "AwmGA", "AMD" };

    Console.WriteLine(
        $"\n{"Algorithm",-14} {"F100",-8} {"F500",-8} {"t(F>0.65),min",-16} {"t(F>0.75),min",-16} {"σ",-8}");
    Console.WriteLine(new string('-', 70));

    foreach (var algName in algos)
    {
        var fitnesses = new List<double>();
        var f100List = new List<double>();
        var tToF065 = new List<double>();
        var tToF075 = new List<double>();

        for (var r = 0; r < runs; r++)
        {
            var objFn = new ObjectiveFunctionService(groups, instructors, disciplines, rooms, assignments, compat);
            var repair = new RepairService(rooms, instructors);
            var opts = new GaOptions { PopulationSize = pop, MaxGenerations = gen, Seed = seed + r };

            var optResult = algName switch
            {
                "AMD" => new AmdService(groups, instructors, disciplines, rooms, assignments, compat, objFn, repair,
                    opts).Run(weights),
                "CpcGA" => new CpcGaService(groups, instructors, disciplines, rooms, assignments, compat, objFn, repair,
                    opts).Run(weights),
                "AwmGA" => new AwmGaService(groups, instructors, disciplines, rooms, assignments, compat, objFn, repair,
                    opts).Run(weights),
                _ => new BaselineGaService(groups, instructors, disciplines, rooms, assignments, compat, objFn, repair,
                    opts).Run(weights)
            };

            fitnesses.Add(optResult.BestMetrics.F);
            f100List.Add(optResult.FitnessHistory.Count >= 100
                ? optResult.FitnessHistory[99]
                : optResult.BestMetrics.F);
            tToF065.Add(optResult.TimeToF065Seconds / 60.0);
            tToF075.Add(optResult.TimeToF075Seconds / 60.0);
        }

        var mean = fitnesses.Average();
        var std = fitnesses.Count > 1 ? Math.Sqrt(fitnesses.Average(v => (v - mean) * (v - mean))) : 0;
        var f100 = f100List.Average();
        var t065 = tToF065.Where(x => x >= 0).DefaultIfEmpty(0).Average();
        var tAvg = tToF075.Where(x => x >= 0).DefaultIfEmpty(0).Average();

        Console.WriteLine($"{algName,-14} {f100,-8:F3} {mean,-8:F3} {t065,-16:F1} {tAvg,-16:F1} {std,-8:F3}");
    }
}

static void PrintHelp()
{
    Console.WriteLine("AnthroDispatch Console Runner");
    Console.WriteLine("  generate  [--seed N]");
    Console.WriteLine("  optimize  [--seed N] [--pop N] [--gen N] [--algorithm BaselineGA|CpcGA|AwmGA|AMD]");
    Console.WriteLine("  ablation  [--seed N] [--pop N] [--gen N] [--runs N]");
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