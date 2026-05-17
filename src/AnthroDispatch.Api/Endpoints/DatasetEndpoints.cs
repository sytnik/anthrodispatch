using AnthroDispatch.Application.Abstractions;
using AnthroDispatch.Application.DataPreparation;
using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.Enums;
using AnthroDispatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AnthroDispatch.Api.Endpoints;

public static class DatasetEndpoints
{
    public static void MapDatasetEndpoints(this IEndpointRouteBuilder app)
    {
        // Legacy simple generator (backward compat)
        app.MapPost("/api/datasets/generate", async (
            GenerateDatasetRequest req,
            IAnthroDispatchMockDataGenerator generator,
            AnthroDispatchDbContext db,
            CancellationToken ct) =>
        {
            var options = new AnthroDispatchGenerationOptions(
                Seed: req.Seed,
                AcademicYears: req.AcademicYears,
                Departments: req.Departments,
                Degrees: req.Degrees,
                EducationalPrograms: req.EducationalPrograms,
                CurriculumPlans: req.CurriculumPlans,
                Terms: req.Terms,
                Groups: req.Groups,
                StudentsApprox: req.StudentsApprox,
                Instructors: req.Instructors,
                Disciplines: req.Disciplines,
                Rooms: req.Rooms,
                InstructorConstraintRate: req.InstructorConstraintRate,
                HealthLimitationRate: req.HealthLimitationRate);

            var dataset = await generator.GenerateAsync(options, ct);

            // Persist to InMemory DB
            await db.AcademicYears.AddRangeAsync(dataset.AcademicYears, ct);
            await db.Degrees.AddRangeAsync(dataset.Degrees, ct);
            await db.Departments.AddRangeAsync(dataset.Departments, ct);
            await db.EducationalPrograms.AddRangeAsync(dataset.EducationalPrograms, ct);
            await db.AcademicCalendars.AddRangeAsync(dataset.AcademicCalendars, ct);
            await db.AcademicCalendarTerms.AddRangeAsync(dataset.AcademicCalendarTerms, ct);
            await db.CurriculumPlans.AddRangeAsync(dataset.CurriculumPlans, ct);
            await db.CurriculumPlanItems.AddRangeAsync(dataset.CurriculumPlanItems, ct);
            await db.CurriculumPlanItemEdges.AddRangeAsync(dataset.CurriculumPlanItemEdges, ct);
            await db.Groups.AddRangeAsync(dataset.Groups, ct);
            await db.Instructors.AddRangeAsync(dataset.Instructors, ct);
            await db.Disciplines.AddRangeAsync(dataset.Disciplines, ct);
            await db.Rooms.AddRangeAsync(dataset.Rooms, ct);
            await db.LearningAssignments.AddRangeAsync(dataset.LearningAssignments, ct);
            await db.LearningAssignmentGroups.AddRangeAsync(dataset.LearningAssignmentGroups, ct);
            await db.LearningAssignmentInstructors.AddRangeAsync(dataset.LearningAssignmentInstructors, ct);
            await db.LearningAssignmentPlanItems.AddRangeAsync(dataset.LearningAssignmentPlanItems, ct);
            await db.HealthLimitations.AddRangeAsync(dataset.HealthLimitations, ct);
            await db.InstructorConstraints.AddRangeAsync(dataset.InstructorConstraints, ct);
            await db.GroupConstraints.AddRangeAsync(dataset.GroupConstraints, ct);
            await db.CognitiveCompatibilities.AddRangeAsync(dataset.CognitiveCompatibilities, ct);

            // Also persist legacy TeachingAssignment stubs so optimizer works
            var assignments = GenerateLegacyAssignments(dataset);
            await db.Assignments.AddRangeAsync(assignments, ct);

            var record = new DatasetRecord
            {
                Id = dataset.Id,
                Seed = req.Seed,
                Groups = dataset.Groups.Count,
                Instructors = dataset.Instructors.Count,
                Disciplines = dataset.Disciplines.Count,
                Rooms = dataset.Rooms.Count,
                Assignments = dataset.LearningAssignments.Count,
                // extended counts
                CurriculumPlans = dataset.CurriculumPlans.Count,
                CurriculumPlanItems = dataset.CurriculumPlanItems.Count,
                CalendarTerms = dataset.AcademicCalendarTerms.Count,
                LearningAssignments = dataset.LearningAssignments.Count,
                InstructorConstraints = dataset.InstructorConstraints.Count,
                HealthLimitations = dataset.HealthLimitations.Count
            };
            await db.Datasets.AddAsync(record, ct);
            await db.SaveChangesAsync(ct);

            return Results.Ok(new
            {
                datasetId = dataset.Id,
                curriculumPlans = dataset.CurriculumPlans.Count,
                curriculumPlanItems = dataset.CurriculumPlanItems.Count,
                calendarTerms = dataset.AcademicCalendarTerms.Count,
                learningAssignments = dataset.LearningAssignments.Count,
                groups = dataset.Groups.Count,
                instructors = dataset.Instructors.Count,
                disciplines = dataset.Disciplines.Count,
                rooms = dataset.Rooms.Count,
                instructorConstraints = dataset.InstructorConstraints.Count,
                healthLimitations = dataset.HealthLimitations.Count
            });
        }).WithName("GenerateDataset").WithTags("Datasets");

        // BuildDispatchProblem
        app.MapPost("/api/datasets/{datasetId:guid}/build-dispatch-problem", async (
            Guid datasetId, IDispatchInputBuilder builder,
            DispatchProblemCache problemCache,
            AnthroDispatchDbContext db, CancellationToken ct) =>
        {
            var datasetRecord = await db.Datasets.FindAsync([datasetId], cancellationToken: ct);
            if (datasetRecord is null) return Results.NotFound("Dataset not found.");

            var dataset = await LoadDataset(datasetId, db, ct);
            if (dataset is null) return Results.NotFound("Dataset data not found.");

            var problem = builder.Build(dataset);

            // store in singleton cache so /api/optimization/run can use dispatchProblemId
            problemCache.Store(problem);

            return Results.Ok(new
            {
                dispatchProblemId = problem.Id,
                atomicUnits = problem.AtomicUnits.Count,
                groups = problem.Groups.Count,
                instructors = problem.Instructors.Count,
                disciplines = problem.Disciplines.Count,
                rooms = problem.Rooms.Count,
                compatibilityScores = problem.CognitiveCompatibilityMatrix.Count,
                instructorConstraints = problem.InstructorConstraints.Count,
                groupConstraints = problem.GroupConstraints.Count,
                healthLimitations = problem.HealthLimitations.Count
            });
        }).WithName("BuildDispatchProblem").WithTags("Datasets");

        app.MapGet("/api/datasets/{datasetId:guid}/summary", async (Guid datasetId, AnthroDispatchDbContext db) =>
        {
            var record = await db.Datasets.FindAsync(datasetId);
            return record is null ? Results.NotFound() : Results.Ok(record);
        }).WithName("GetDatasetSummary").WithTags("Datasets");
    }

    private static async Task<AnthroDispatchDataset?> LoadDataset(
        Guid datasetId, AnthroDispatchDbContext db, CancellationToken ct)
    {
        // Load all entities that belong to this dataset (using the InMemory DB)
        return new AnthroDispatchDataset
        {
            Id = datasetId,
            AcademicYears = await db.AcademicYears.ToListAsync(ct),
            Degrees = await db.Degrees.ToListAsync(ct),
            Departments = await db.Departments.ToListAsync(ct),
            EducationalPrograms = await db.EducationalPrograms.ToListAsync(ct),
            AcademicCalendars = await db.AcademicCalendars.ToListAsync(ct),
            AcademicCalendarTerms = await db.AcademicCalendarTerms.ToListAsync(ct),
            CurriculumPlans = await db.CurriculumPlans.ToListAsync(ct),
            CurriculumPlanItems = await db.CurriculumPlanItems.ToListAsync(ct),
            CurriculumPlanItemEdges = await db.CurriculumPlanItemEdges.ToListAsync(ct),
            Groups = await db.Groups.ToListAsync(ct),
            Instructors = await db.Instructors.ToListAsync(ct),
            Disciplines = await db.Disciplines.ToListAsync(ct),
            Rooms = await db.Rooms.ToListAsync(ct),
            LearningAssignments = await db.LearningAssignments.ToListAsync(ct),
            LearningAssignmentGroups = await db.LearningAssignmentGroups.ToListAsync(ct),
            LearningAssignmentInstructors = await db.LearningAssignmentInstructors.ToListAsync(ct),
            LearningAssignmentPlanItems = await db.LearningAssignmentPlanItems.ToListAsync(ct),
            HealthLimitations = await db.HealthLimitations.ToListAsync(ct),
            InstructorConstraints = await db.InstructorConstraints.ToListAsync(ct),
            GroupConstraints = await db.GroupConstraints.ToListAsync(ct),
            CognitiveCompatibilities = await db.CognitiveCompatibilities.ToListAsync(ct)
        };
    }

    /// <summary>
    /// Generate backward-compatible TeachingAssignment stubs from LearningAssignment data
    /// so the existing GA optimizer still works.
    /// </summary>
    private static List<TeachingAssignment> GenerateLegacyAssignments(AnthroDispatchDataset dataset)
    {
        return (from la in dataset.LearningAssignments
            let groupId =
                dataset.LearningAssignmentGroups.FirstOrDefault(x => x.LearningAssignmentId == la.Id)?.GroupId ??
                Guid.Empty
            let instructorId =
                dataset.LearningAssignmentInstructors.FirstOrDefault(x => x.LearningAssignmentId == la.Id)
                    ?.InstructorId ?? Guid.Empty
            let disciplineId =
                dataset.LearningAssignmentPlanItems.FirstOrDefault(x => x.LearningAssignmentId == la.Id)
                    ?.DisciplineId ?? Guid.Empty
            where groupId != Guid.Empty && disciplineId != Guid.Empty
            let hours = la.HoursFirstPart + la.HoursSecondPart
            let requiredPeriods = Math.Clamp((int)Math.Ceiling(hours / 2.0), 1, 6)
            select new TeachingAssignment
            {
                Id = la.Id,
                GroupId = groupId,
                InstructorId = instructorId,
                DisciplineId = disciplineId,
                ClassType = la.LessonType switch
                {
                    LessonType.Laboratory => ClassType.Laboratory,
                    LessonType.Practice => ClassType.Practice,
                    LessonType.Seminar => ClassType.Seminar,
                    LessonType.Online => ClassType.Online,
                    _ => ClassType.Lecture
                },
                RequiredPeriods = requiredPeriods
            }).ToList();
    }
}