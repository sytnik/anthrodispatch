using AnthroDispatch.Application.Abstractions;
using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.Entities.Anthropocentric;
using AnthroDispatch.Domain.Entities.Operational;
using AnthroDispatch.Domain.Enums;
using Bogus;

namespace AnthroDispatch.Infrastructure.MockData;

/// <summary>
/// Full AnthroDispatch operational mock data generator.
/// Generates curriculum plans, calendar terms, learning assignments,
/// age profiles, health limitations, and instructor/group constraints.
/// </summary>
public sealed class AnthroDispatchMockDataGenerator : IAnthroDispatchMockDataGenerator
{
    private static readonly string[] DeptNames =
    [
        "Aerospace Engineering", "Software Engineering", "Avionics Systems",
        "Mathematics", "Physics", "Humanities"
    ];

    private static readonly string[] ProgramNames =
    [
        "Aerospace Engineering", "Software Engineering", "Avionics Systems", "Mechanical Engineering"
    ];

    private static readonly string[] DisciplinePool =
    [
        "Aircraft Systems Design", "Aerospace Materials", "Flight Dynamics",
        "Avionics Architecture", "Control Systems", "Engineering Mathematics",
        "Numerical Methods", "Programming in C#", "Data Structures and Algorithms",
        "Computer-Aided Design", "Hydraulics and Pneumatics", "Robotics",
        "Digital Signal Processing", "Technical English", "Engineering Ethics",
        "Project Management", "Human Factors Engineering", "Physics",
        "Probability and Statistics", "Embedded Systems",
        "Thermodynamics", "Structural Analysis", "Aerospace Propulsion",
        "Advanced Algorithms", "Operating Systems", "Database Systems",
        "Machine Learning", "Signal Theory", "Systems Engineering",
        "Quality Management", "Manufacturing Processes", "Aeronautical Engineering",
        "Fluid Mechanics", "Navigation Systems", "Electronic Warfare",
        "Research Methods", "Technical Writing", "Innovation Management",
        "Applied Mathematics", "Satellite Systems", "Cybersecurity", "Cloud Computing"
    ];

    private static readonly string[] GroupPrefixes = ["AE", "CS", "AV", "ME", "SE", "EE"];

    private static readonly (ChronotypeCategory Cat, double StudentWeight, double InstructorWeight)[]
        ChronotypeDistribution =
        [
            (ChronotypeCategory.DefiniteMorning, 0.08, 0.22),
            (ChronotypeCategory.ModerateMorning, 0.24, 0.31),
            (ChronotypeCategory.Intermediate, 0.39, 0.32),
            (ChronotypeCategory.ModerateEvening, 0.22, 0.13),
            (ChronotypeCategory.DefiniteEvening, 0.07, 0.02)
        ];

    public Task<AnthroDispatchDataset> GenerateAsync(
        AnthroDispatchGenerationOptions options,
        CancellationToken cancellationToken = default)
    {
        Randomizer.Seed = new Random(options.Seed);
        var faker = new Faker { Random = new Randomizer(options.Seed) };

        // ── Step 1-6: operational skeleton ────────────────────────────────────
        var academicYears = GenerateAcademicYears(options.AcademicYears);
        var degrees = GenerateDegrees(options.Degrees);
        var departments = GenerateDepartments(options.Departments);
        var programs = GeneratePrograms(faker, options.EducationalPrograms, degrees, departments, academicYears);
        var calendars = GenerateCalendars(faker, programs, academicYears, degrees);
        var calendarTerms = GenerateCalendarTerms(faker, calendars, options.Terms);

        // ── Step 7-10: curriculum ──────────────────────────────────────────────
        var disciplines = GenerateDisciplines(faker, options.Disciplines);
        var plans = GenerateCurriculumPlans(faker, options.CurriculumPlans, programs, calendars, academicYears);
        var planItems = GeneratePlanItems(faker, plans, disciplines, calendarTerms);
        var edges = GeneratePrerequisiteEdges(faker, planItems);

        // ── Step 11-15: actors and rooms ──────────────────────────────────────
        var isBachelor = degrees.Any(d => d.ShortName == "BSc");
        var groups = GenerateGroups(faker, options.Groups, options.StudentsApprox, programs, plans, isBachelor);
        var instructors = GenerateInstructors(faker, options.Instructors, departments);
        var healthLimits = GenerateHealthLimitations(faker, instructors, options.HealthLimitationRate);
        var rooms = GenerateRooms(faker, options.Rooms);
        var instrConstr = GenerateInstructorConstraints(faker, instructors, options.InstructorConstraintRate, rooms);

        // ── Step 16-21: assignments and compatibility ─────────────────────────
        var assignments =
            GenerateLearningAssignments(faker, planItems, calendarTerms, departments, academicYears, degrees);
        var assignmentGroups = GenerateAssignmentGroups(faker, assignments, groups);
        var assignmentInstrs = GenerateAssignmentInstructors(faker, assignments, instructors);
        var assignmentItems = GenerateAssignmentPlanItems(assignments, planItems);
        var groupConstr = GenerateGroupConstraints(faker, groups);
        var compat = GenerateCompatibilityMatrix(disciplines);

        var dataset = new AnthroDispatchDataset
        {
            AcademicYears = academicYears,
            Degrees = degrees,
            Departments = departments,
            EducationalPrograms = programs,
            AcademicCalendars = calendars,
            AcademicCalendarTerms = calendarTerms,
            CurriculumPlans = plans,
            CurriculumPlanItems = planItems,
            CurriculumPlanItemEdges = edges,
            Groups = groups,
            Instructors = instructors,
            Disciplines = disciplines,
            Rooms = rooms,
            LearningAssignments = assignments,
            LearningAssignmentGroups = assignmentGroups,
            LearningAssignmentInstructors = assignmentInstrs,
            LearningAssignmentPlanItems = assignmentItems,
            HealthLimitations = healthLimits,
            InstructorConstraints = instrConstr,
            GroupConstraints = groupConstr,
            CognitiveCompatibilities = compat
        };

        return Task.FromResult(dataset);
    }

    // ── Generators ────────────────────────────────────────────────────────────

    private static List<AcademicYear> GenerateAcademicYears(int count)
    {
        var years = new List<AcademicYear>();
        var startYear = 2024;
        for (var i = 0; i < count; i++)
        {
            var y = startYear + i;
            years.Add(new AcademicYear { Id = Guid.NewGuid(), StartYear = y, Name = $"{y}/{y + 1}" });
        }

        return years;
    }

    private static List<Degree> GenerateDegrees(int count)
    {
        var pool = new[] { ("Bachelor of Science", "BSc"), ("Master of Science", "MSc") };
        return pool.Take(count).Select(d => new Degree { Id = Guid.NewGuid(), FullName = d.Item1, ShortName = d.Item2 })
            .ToList();
    }

    private static List<Department> GenerateDepartments(int count)
    {
        var depts = new List<Department>();
        for (var i = 0; i < count; i++)
        {
            var name = i < DeptNames.Length ? DeptNames[i] : $"Department {i + 1}";
            depts.Add(new Department { Id = Guid.NewGuid(), Name = name, Number = 100 + i * 10 });
        }

        return depts;
    }

    private static List<EducationalProgram> GeneratePrograms(
        Faker f, int count,
        List<Degree> degrees, List<Department> departments, List<AcademicYear> years)
    {
        var progs = new List<EducationalProgram>();
        for (var i = 0; i < count; i++)
        {
            var name = i < ProgramNames.Length ? ProgramNames[i] : $"Program {i + 1}";
            progs.Add(new EducationalProgram
            {
                Id = Guid.NewGuid(),
                FullName = name,
                ShortName = name.Split(' ').Select(w => w[..1]).Aggregate("", (a, b) => a + b),
                DegreeId = f.PickRandom(degrees).Id,
                DepartmentId = f.PickRandom(departments).Id,
                StartYearId = f.PickRandom(years).Id
            });
        }

        return progs;
    }

    private static List<AcademicCalendar> GenerateCalendars(
        Faker f, List<EducationalProgram> programs, List<AcademicYear> years, List<Degree> degrees)
    {
        return programs.Select(p => new AcademicCalendar
        {
            Id = Guid.NewGuid(),
            Name = $"Calendar for {p.FullName}",
            AcademicYearId = f.PickRandom(years).Id,
            DegreeId = f.PickRandom(degrees).Id
        }).ToList();
    }

    private static List<AcademicCalendarTerm> GenerateCalendarTerms(
        Faker f, List<AcademicCalendar> calendars, int termsPerCalendar)
    {
        var terms = new List<AcademicCalendarTerm>();
        foreach (var cal in calendars)
        {
            for (var term = 1; term <= termsPerCalendar; term++)
            {
                terms.Add(new AcademicCalendarTerm
                {
                    Id = Guid.NewGuid(),
                    CalendarId = cal.Id,
                    Term = term,
                    PartOneWeeks = f.Random.Int(8, 10),
                    PartTwoWeeks = f.Random.Int(7, 9),
                    TermOccurrenceYearId = cal.AcademicYearId
                });
            }
        }

        return terms;
    }

    private static List<Discipline> GenerateDisciplines(Faker f, int count)
    {
        var discs = new List<Discipline>();
        for (var i = 0; i < count; i++)
        {
            var name = i < DisciplinePool.Length ? DisciplinePool[i] : $"Advanced Topic {i + 1}";
            discs.Add(new Discipline
            {
                Id = Guid.NewGuid(),
                Code = $"D{i + 1:D3}",
                Name = name,
                ProcessType = f.PickRandom<CognitiveProcessType>(),
                LoadLevel = f.PickRandom<CognitiveLoadLevel>(),
                Domain = f.PickRandom<DisciplineDomain>()
            });
        }

        return discs;
    }

    private static List<CurriculumPlan> GenerateCurriculumPlans(
        Faker f, int count,
        List<EducationalProgram> programs, List<AcademicCalendar> calendars, List<AcademicYear> years)
    {
        var plans = new List<CurriculumPlan>();
        for (var i = 0; i < count; i++)
        {
            var prog = f.PickRandom(programs);
            var cal = f.PickRandom(calendars);
            plans.Add(new CurriculumPlan
            {
                Id = Guid.NewGuid(),
                Name = $"Plan {prog.ShortName} {i + 1}",
                EducationalProgramId = prog.Id,
                StartYearId = f.PickRandom(years).Id,
                CalendarId = cal.Id,
                EducationForm = f.PickRandom<EducationForm>(),
                EducationLanguage = EducationLanguage.English,
                ReadyForScheduling = true,
                IsLocked = false
            });
        }

        return plans;
    }

    private static List<CurriculumPlanItem> GeneratePlanItems(
        Faker f, List<CurriculumPlan> plans, List<Discipline> disciplines, List<AcademicCalendarTerm> terms)
    {
        var items = new List<CurriculumPlanItem>();
        var termsPerCalendar = terms.GroupBy(t => t.CalendarId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var plan in plans)
        {
            var calTerms = termsPerCalendar.GetValueOrDefault(plan.CalendarId, terms.Take(4).ToList());
            var termCount = calTerms.Count > 0 ? calTerms.Max(t => t.Term) : 4;

            for (var term = 1; term <= termCount; term++)
            {
                var itemsInTerm = f.Random.Int(5, 8);
                var termDiscs = f.Random.ListItems(disciplines, Math.Min(itemsInTerm, disciplines.Count));
                foreach (var disc in termDiscs)
                {
                    items.Add(new CurriculumPlanItem
                    {
                        Id = Guid.NewGuid(),
                        CurriculumPlanId = plan.Id,
                        DisciplineId = disc.Id,
                        DepartmentReaderId = Guid.NewGuid(),
                        Term = term,
                        Credits = f.Random.Int(3, 6),
                        LecturePerWeekFirst = f.Random.Int(0, 4),
                        LecturePerWeekSecond = f.Random.Int(0, 4),
                        LabWorkPerWeekFirst = f.Random.Int(0, 3),
                        LabWorkPerWeekSecond = f.Random.Int(0, 3),
                        PracticalWorkPerWeekFirst = f.Random.Int(0, 3),
                        PracticalWorkPerWeekSecond = f.Random.Int(0, 3),
                        HasExam = f.Random.Bool(0.6f),
                        HasTest = f.Random.Bool(0.4f)
                    });
                }
            }
        }

        return items;
    }

    private static List<CurriculumPlanItemEdge> GeneratePrerequisiteEdges(
        Faker f, List<CurriculumPlanItem> items)
    {
        var edges = new List<CurriculumPlanItemEdge>();
        // Edges predominantly from earlier terms to later terms (prerequisite direction)
        var byPlan = items.GroupBy(i => i.CurriculumPlanId);
        foreach (var group in byPlan)
        {
            var sorted = group.OrderBy(i => i.Term).ToList();
            for (var i = 0; i < sorted.Count - 1; i++)
            {
                if (sorted[i].Term < sorted[i + 1].Term && f.Random.Bool(0.25f))
                {
                    edges.Add(new CurriculumPlanItemEdge
                    {
                        Id = Guid.NewGuid(),
                        ParentPlanItemId = sorted[i].Id,
                        ChildPlanItemId = sorted[i + 1].Id
                    });
                }
            }
        }

        return edges;
    }

    private static List<AcademicGroup> GenerateGroups(
        Faker f, int count, int studentsApprox,
        List<EducationalProgram> programs, List<CurriculumPlan> plans, bool isBachelor)
    {
        var groups = new List<AcademicGroup>();
        var idx = 100;
        for (var i = 0; i < count; i++)
        {
            var prefix = GroupPrefixes[i % GroupPrefixes.Length];
            var chronotype = SampleChronotype(f, isStudent: true);
            var avgAge = isBachelor ? f.Random.Double(18, 24) : f.Random.Double(21, 28);
            var prog = f.PickRandom(programs);
            var plan = plans.FirstOrDefault(p => p.EducationalProgramId == prog.Id);

            groups.Add(new AcademicGroup
            {
                Id = Guid.NewGuid(),
                Code = $"{prefix}-{idx++}",
                ProgramName = prog.FullName,
                EducationalProgramId = prog.Id,
                CurriculumPlanId = plan?.Id,
                StudentCount = studentsApprox / count + f.Random.Int(-3, 3),
                Chronotype = chronotype,
                MeanMeqScore = ChronotypeToMeq(chronotype) + f.Random.Double(-2, 2),
                AverageAge = avgAge,
                AgeStdDev = f.Random.Double(0.8, 2.5),
                HealthLimitationIds = []
            });
        }

        return groups;
    }

    private static List<Instructor> GenerateInstructors(Faker f, int count, List<Department> departments)
    {
        var instructors = new List<Instructor>();
        for (var i = 0; i < count; i++)
        {
            var chronotype = SampleChronotype(f, isStudent: false);
            var dept = f.PickRandom(departments);
            instructors.Add(new Instructor
            {
                Id = Guid.NewGuid(),
                FullName = f.Name.FullName(),
                Department = dept.Name,
                DepartmentId = dept.Id,
                Chronotype = chronotype,
                MeqScore = ChronotypeToMeq(chronotype) + f.Random.Double(-3, 3),
                Age = f.Random.Int(25, 70),
                MaxClassesPerDay = f.Random.Int(3, 5),
                MaxConsecutiveClasses = f.Random.Int(2, 4)
            });
        }

        return instructors;
    }

    private static List<HealthLimitation> GenerateHealthLimitations(Faker f, List<Instructor> instructors, double rate)
    {
        var limits = new List<HealthLimitation>();
        foreach (var instr in instructors)
        {
            if (f.Random.Double() < rate)
            {
                limits.Add(new HealthLimitation
                {
                    Id = Guid.NewGuid(),
                    InstructorId = instr.Id,
                    Type = f.PickRandom<HealthLimitationType>(),
                    Severity = f.PickRandom<HealthLimitationSeverity>(),
                    Description = "Generated health constraint",
                    IsHardConstraint = f.Random.Bool(0.3f)
                });
            }
        }

        return limits;
    }

    private static List<InstructorConstraint> GenerateInstructorConstraints(
        Faker f, List<Instructor> instructors, double rate, List<Room> rooms)
    {
        var constraints = new List<InstructorConstraint>();
        foreach (var instr in instructors)
        {
            if (f.Random.Double() < rate)
            {
                var cType = f.PickRandom(
                    ConstraintType.AvoidFirstPeriod, ConstraintType.UnavailableDay,
                    ConstraintType.MaxClassesPerDay, ConstraintType.MaxConsecutiveClasses,
                    ConstraintType.AvoidLatePeriods, ConstraintType.RequiredBreakAfterClass,
                    ConstraintType.PreferredPeriods, ConstraintType.RoomOrBuildingRestriction,
                    ConstraintType.OnlineOnly, ConstraintType.HealthRelated);

                var preferredPeriod = cType == ConstraintType.PreferredPeriods
                    ? f.Random.Int(2, 6)
                    : (int?)null;
                var constraintPeriod = cType == ConstraintType.MaxConsecutiveClasses
                    ? instr.MaxConsecutiveClasses
                    : preferredPeriod;
                var roomId = cType == ConstraintType.RoomOrBuildingRestriction && rooms.Count > 0
                    ? f.PickRandom(rooms).Id
                    : (Guid?)null;

                constraints.Add(new InstructorConstraint
                {
                    Id = Guid.NewGuid(),
                    InstructorId = instr.Id,
                    Type = cType,
                    Severity = f.Random.Bool(0.4f) ? ConstraintSeverity.Hard : ConstraintSeverity.Soft,
                    Day = cType == ConstraintType.UnavailableDay ? f.Random.Int(1, 6) : null,
                    Period = constraintPeriod,
                    RoomId = roomId
                });
            }
        }

        return constraints;
    }

    private static List<Room> GenerateRooms(Faker f, int count)
    {
        var rooms = new List<Room>();
        for (var i = 0; i < count; i++)
        {
            var type = f.PickRandom<RoomType>();
            rooms.Add(new Room
            {
                Id = Guid.NewGuid(),
                Code = $"R{i + 1:D3}",
                Type = type,
                Capacity = type == RoomType.LectureHall ? f.Random.Int(100, 180)
                    : type == RoomType.Laboratory ? f.Random.Int(20, 40)
                    : f.Random.Int(25, 60)
            });
        }

        return rooms;
    }

    private static List<LearningAssignment> GenerateLearningAssignments(
        Faker f, List<CurriculumPlanItem> planItems,
        List<AcademicCalendarTerm> calendarTerms,
        List<Department> departments, List<AcademicYear> years, List<Degree> degrees)
    {
        var assignments = new List<LearningAssignment>();
        var lessonTypes = new[] { LessonType.Lecture, LessonType.Practice, LessonType.Laboratory };

        foreach (var item in planItems)
        {
            var lessonType = f.PickRandom(lessonTypes);
            var term = calendarTerms.FirstOrDefault(t => t.Term == item.Term)
                       ?? calendarTerms.FirstOrDefault()
                       ?? new AcademicCalendarTerm { PartOneWeeks = 8, PartTwoWeeks = 8 };

            var hoursFirst = lessonType switch
            {
                LessonType.Lecture => item.LecturePerWeekFirst * term.PartOneWeeks,
                LessonType.Laboratory => item.LabWorkPerWeekFirst * term.PartOneWeeks,
                _ => item.PracticalWorkPerWeekFirst * term.PartOneWeeks
            };
            var hoursSecond = lessonType switch
            {
                LessonType.Lecture => item.LecturePerWeekSecond * term.PartTwoWeeks,
                LessonType.Laboratory => item.LabWorkPerWeekSecond * term.PartTwoWeeks,
                _ => item.PracticalWorkPerWeekSecond * term.PartTwoWeeks
            };

            if (hoursFirst + hoursSecond == 0) continue;

            assignments.Add(new LearningAssignment
            {
                Id = Guid.NewGuid(),
                LessonType = lessonType,
                HoursFirstPart = hoursFirst,
                HoursSecondPart = hoursSecond,
                DepartmentId = f.PickRandom(departments).Id,
                AcademicYearId = f.PickRandom(years).Id,
                Term = item.Term,
                EducationForm = EducationForm.FullTime,
                EducationLanguage = EducationLanguage.English,
                DegreeId = f.PickRandom(degrees).Id
            });
        }

        return assignments;
    }

    private static List<LearningAssignmentGroup> GenerateAssignmentGroups(
        Faker f, List<LearningAssignment> assignments, List<AcademicGroup> groups)
    {
        var links = new List<LearningAssignmentGroup>();
        foreach (var la in assignments)
        {
            var group = f.PickRandom(groups);
            links.Add(new LearningAssignmentGroup { LearningAssignmentId = la.Id, GroupId = group.Id });
        }

        return links;
    }

    private static List<LearningAssignmentInstructor> GenerateAssignmentInstructors(
        Faker f, List<LearningAssignment> assignments, List<Instructor> instructors)
    {
        var links = new List<LearningAssignmentInstructor>();
        foreach (var la in assignments)
        {
            var instr = f.PickRandom(instructors);
            links.Add(new LearningAssignmentInstructor { LearningAssignmentId = la.Id, InstructorId = instr.Id });
        }

        return links;
    }

    private static List<LearningAssignmentPlanItem> GenerateAssignmentPlanItems(
        List<LearningAssignment> assignments, List<CurriculumPlanItem> planItems)
    {
        var links = new List<LearningAssignmentPlanItem>();
        // Simple 1-to-1: assignment index matches plan item index (both bounded)
        for (var i = 0; i < Math.Min(assignments.Count, planItems.Count); i++)
        {
            links.Add(new LearningAssignmentPlanItem
            {
                LearningAssignmentId = assignments[i].Id,
                CurriculumPlanItemId = planItems[i].Id,
                DisciplineId = planItems[i].DisciplineId
            });
        }

        return links;
    }

    private static List<GroupConstraint> GenerateGroupConstraints(Faker f, List<AcademicGroup> groups)
    {
        var constraints = new List<GroupConstraint>();
        foreach (var group in groups)
        {
            if (f.Random.Bool(0.15f))
            {
                constraints.Add(new GroupConstraint
                {
                    Id = Guid.NewGuid(),
                    GroupId = group.Id,
                    Type = f.PickRandom(ConstraintType.AvoidFirstPeriod, ConstraintType.UnavailableDay),
                    Severity = ConstraintSeverity.Soft,
                    Day = f.Random.Bool(0.5f) ? f.Random.Int(1, 6) : null
                });
            }
        }

        return constraints;
    }

    private static List<CognitiveCompatibility> GenerateCompatibilityMatrix(List<Discipline> disciplines)
    {
        var result = new List<CognitiveCompatibility>();
        foreach (var d1 in disciplines)
        foreach (var d2 in disciplines)
        {
            if (d1.Id == d2.Id) continue;
            var score = ComputeCompatibility(d1, d2);
            result.Add(new CognitiveCompatibility
            {
                Id = Guid.NewGuid(),
                FromDisciplineId = d1.Id,
                ToDisciplineId = d2.Id,
                Score = Math.Clamp(score, -1.0, 1.0)
            });
        }

        return result;
    }

    private static double ComputeCompatibility(Discipline from, Discipline to)
    {
        var typeScore = (from.ProcessType, to.ProcessType) switch
        {
            (CognitiveProcessType.Analytical, CognitiveProcessType.Creative) => 0.60,
            (CognitiveProcessType.Analytical, CognitiveProcessType.Analytical) => -0.40,
            (CognitiveProcessType.Analytical, CognitiveProcessType.Communicative) => -0.20,
            (CognitiveProcessType.Creative, CognitiveProcessType.Analytical) => 0.40,
            (CognitiveProcessType.Mnemonic, CognitiveProcessType.Mnemonic) => -0.70,
            (CognitiveProcessType.Communicative, CognitiveProcessType.Communicative) => -0.20,
            _ => 0.00
        };
        var loadScore = (from.LoadLevel, to.LoadLevel) switch
        {
            (CognitiveLoadLevel.High, CognitiveLoadLevel.Low) => 0.90,
            (CognitiveLoadLevel.High, CognitiveLoadLevel.Medium) => 0.30,
            (CognitiveLoadLevel.High, CognitiveLoadLevel.High) => -0.70,
            (CognitiveLoadLevel.Medium, CognitiveLoadLevel.Low) => 0.40,
            (CognitiveLoadLevel.Medium, CognitiveLoadLevel.High) => -0.30,
            (CognitiveLoadLevel.Low, CognitiveLoadLevel.High) => 0.20,
            _ => 0.00
        };
        var domainScore = (from.Domain, to.Domain) switch
        {
            (DisciplineDomain.NaturalScience, DisciplineDomain.Technical) => 0.50,
            (DisciplineDomain.Technical, DisciplineDomain.NaturalScience) => 0.40,
            (DisciplineDomain.Humanities, DisciplineDomain.Arts) => 0.40,
            (DisciplineDomain.Arts, DisciplineDomain.Humanities) => 0.40,
            (DisciplineDomain.Technical, DisciplineDomain.Humanities) => -0.10,
            (DisciplineDomain.Humanities, DisciplineDomain.Technical) => -0.20,
            var (fr, t) when fr == t => 0.30,
            _ => 0.00
        };
        return 0.40 * typeScore + 0.35 * loadScore + 0.25 * domainScore;
    }

    private static ChronotypeCategory SampleChronotype(Faker f, bool isStudent)
    {
        var r = f.Random.Double();
        double cumulative = 0;
        foreach (var (cat, sw, iw) in ChronotypeDistribution)
        {
            cumulative += isStudent ? sw : iw;
            if (r <= cumulative) return cat;
        }

        return ChronotypeCategory.Intermediate;
    }

    private static double ChronotypeToMeq(ChronotypeCategory c) => c switch
    {
        ChronotypeCategory.DefiniteMorning => 70,
        ChronotypeCategory.ModerateMorning => 59,
        ChronotypeCategory.Intermediate => 50,
        ChronotypeCategory.ModerateEvening => 41,
        ChronotypeCategory.DefiniteEvening => 30,
        _ => 50
    };
}