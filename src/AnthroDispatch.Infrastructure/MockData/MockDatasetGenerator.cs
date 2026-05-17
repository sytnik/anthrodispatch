using AnthroDispatch.Application.Abstractions;
using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.Enums;
using Bogus;

namespace AnthroDispatch.Infrastructure.MockData;

public sealed class MockDatasetGenerator : IMockDatasetGenerator
{
    private static readonly string[] DisciplineNames =
    [
        "Aircraft Systems Design", "Aerospace Materials", "Flight Dynamics",
        "Avionics Architecture", "Control Systems", "Engineering Mathematics",
        "Numerical Methods", "Programming in C#", "Data Structures and Algorithms",
        "Computer-Aided Design", "Hydraulics and Pneumatics", "Robotics",
        "Digital Signal Processing", "Technical English", "Engineering Ethics",
        "Project Management", "Human Factors Engineering", "Physics",
        "Probability and Statistics", "Embedded Systems"
    ];

    private static readonly string[] GroupPrefixes = ["AE", "CS", "AV", "ME", "SE", "EE"];

    private static readonly string[] Departments =
    [
        "Aerospace Engineering", "Computer Science", "Avionics", "Mechanical Engineering", "Systems Engineering",
        "Electrical Engineering"
    ];

    private static readonly (ChronotypeCategory Cat, double StudentWeight, double InstructorWeight)[]
        ChronotypeDistribution =
        [
            (ChronotypeCategory.DefiniteMorning, 0.08, 0.22),
            (ChronotypeCategory.ModerateMorning, 0.24, 0.31),
            (ChronotypeCategory.Intermediate, 0.39, 0.32),
            (ChronotypeCategory.ModerateEvening, 0.22, 0.13),
            (ChronotypeCategory.DefiniteEvening, 0.07, 0.02)
        ];

    public Task<DatasetGenerationResult> GenerateAsync(
        DatasetGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        Randomizer.Seed = new Random(request.Seed);
        var faker = new Faker() { Random = new Randomizer(request.Seed) };

        // Groups
        var groups = GenerateGroups(faker, request.Groups, request.StudentsApprox);

        // Instructors
        var instructors = GenerateInstructors(faker, request.Instructors);

        // Disciplines
        var disciplines = GenerateDisciplines(faker, request.Disciplines);

        // Rooms
        var rooms = GenerateRooms(faker, request.Rooms);

        // Assignments: each group gets classes for subset of disciplines
        var assignments = GenerateAssignments(faker, groups, instructors, disciplines);

        // Cognitive compatibility matrix
        var compatibilities = GenerateCompatibilityMatrix(disciplines);

        var result = new DatasetGenerationResult(
            Guid.NewGuid(),
            groups, instructors, disciplines, rooms, assignments, compatibilities);

        return Task.FromResult(result);
    }

    private static List<AcademicGroup> GenerateGroups(Faker faker, int count, int studentsApprox)
    {
        var groups = new List<AcademicGroup>();
        var idx = 100;
        for (var i = 0; i < count; i++)
        {
            var prefix = GroupPrefixes[i % GroupPrefixes.Length];
            var chronotype = SampleChronotype(faker, isStudent: true);
            groups.Add(new AcademicGroup
            {
                Id = Guid.NewGuid(),
                Code = $"{prefix}-{idx++}",
                ProgramName = $"{Departments[i % Departments.Length]} Program",
                StudentCount = studentsApprox / count + faker.Random.Int(-3, 3),
                Chronotype = chronotype,
                MeanMeqScore = ChronotypeToMeq(chronotype) + faker.Random.Double(-2, 2)
            });
        }

        return groups;
    }

    private static List<Instructor> GenerateInstructors(Faker faker, int count)
    {
        var instructors = new List<Instructor>();
        for (var i = 0; i < count; i++)
        {
            var chronotype = SampleChronotype(faker, isStudent: false);
            instructors.Add(new Instructor
            {
                Id = Guid.NewGuid(),
                FullName = faker.Name.FullName(),
                Department = faker.PickRandom(Departments),
                Chronotype = chronotype,
                MeqScore = ChronotypeToMeq(chronotype) + faker.Random.Double(-3, 3),
                MaxClassesPerDay = faker.Random.Int(3, 5)
            });
        }

        return instructors;
    }

    private static List<Discipline> GenerateDisciplines(Faker faker, int count)
    {
        var disciplines = new List<Discipline>();
        var usedNames = new HashSet<string>();
        var allNames = DisciplineNames.ToList();

        for (var i = 0; i < count; i++)
        {
            string name;
            if (i < allNames.Count && !usedNames.Contains(allNames[i]))
            {
                name = allNames[i];
            }
            else
            {
                name = $"Advanced Topic {i + 1}";
            }

            usedNames.Add(name);

            disciplines.Add(new Discipline
            {
                Id = Guid.NewGuid(),
                Code = $"D{i + 1:D3}",
                Name = name,
                ProcessType = faker.PickRandom<CognitiveProcessType>(),
                LoadLevel = faker.PickRandom<CognitiveLoadLevel>(),
                Domain = faker.PickRandom<DisciplineDomain>()
            });
        }

        return disciplines;
    }

    private static List<Room> GenerateRooms(Faker faker, int count)
    {
        var rooms = new List<Room>();
        for (var i = 0; i < count; i++)
        {
            var type = faker.PickRandom<RoomType>();
            rooms.Add(new Room
            {
                Id = Guid.NewGuid(),
                Code = $"R{i + 1:D3}",
                Type = type,
                Capacity = type == RoomType.LectureHall ? faker.Random.Int(100, 300)
                    : type == RoomType.Laboratory ? faker.Random.Int(20, 40)
                    : faker.Random.Int(25, 60)
            });
        }

        return rooms;
    }

    private static List<TeachingAssignment> GenerateAssignments(
        Faker faker,
        List<AcademicGroup> groups,
        List<Instructor> instructors,
        List<Discipline> disciplines)
    {
        var assignments = new List<TeachingAssignment>();
        var disciplinesPerGroup =
            Math.Min(disciplines.Count, Math.Max(3, disciplines.Count / Math.Max(groups.Count / 2, 1)));

        foreach (var group in groups)
        {
            var groupDisciplines = faker.Random.ListItems(disciplines, disciplinesPerGroup);
            foreach (var discipline in groupDisciplines)
            {
                var instructor = faker.Random.ListItem(instructors);
                var classType = discipline.Domain switch
                {
                    DisciplineDomain.Technical => faker.PickRandom(ClassType.Lecture, ClassType.Laboratory),
                    DisciplineDomain.NaturalScience => faker.PickRandom(ClassType.Lecture, ClassType.Practice),
                    DisciplineDomain.Humanities => faker.PickRandom(ClassType.Lecture, ClassType.Seminar),
                    _ => faker.PickRandom<ClassType>()
                };

                assignments.Add(new TeachingAssignment
                {
                    Id = Guid.NewGuid(),
                    GroupId = group.Id,
                    InstructorId = instructor.Id,
                    DisciplineId = discipline.Id,
                    ClassType = classType,
                    RequiredPeriods = faker.Random.Int(1, 3)
                });
            }
        }

        return assignments;
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
            var (f, t) when f == t => 0.30,
            _ => 0.00
        };

        return 0.40 * typeScore + 0.35 * loadScore + 0.25 * domainScore;
    }

    private static ChronotypeCategory SampleChronotype(Faker faker, bool isStudent)
    {
        var r = faker.Random.Double();
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