namespace AnthroDispatch.Api.Endpoints;

public sealed record GenerateDatasetRequest(
    int Seed = 42,
    int AcademicYears = 2,
    int Departments = 6,
    int Degrees = 2,
    int EducationalPrograms = 4,
    int CurriculumPlans = 4,
    int Terms = 8,
    int Groups = 18,
    int StudentsApprox = 400,
    int Instructors = 60,
    int Disciplines = 20,
    int Rooms = 25,
    double InstructorConstraintRate = 0.35,
    double HealthLimitationRate = 0.10);