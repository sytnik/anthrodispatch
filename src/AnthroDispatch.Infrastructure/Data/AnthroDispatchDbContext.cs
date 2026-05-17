using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.Entities.Anthropocentric;
using AnthroDispatch.Domain.Entities.Operational;
using Microsoft.EntityFrameworkCore;

namespace AnthroDispatch.Infrastructure.Data;

public sealed class AnthroDispatchDbContext(DbContextOptions<AnthroDispatchDbContext> options) : DbContext(options)
{
    // ── Existing core sets ────────────────────────────────────────────────────
    public DbSet<AcademicGroup> Groups => Set<AcademicGroup>();
    public DbSet<Instructor> Instructors => Set<Instructor>();
    public DbSet<Discipline> Disciplines => Set<Discipline>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<TeachingAssignment> Assignments => Set<TeachingAssignment>();
    public DbSet<CognitiveCompatibility> CognitiveCompatibilities => Set<CognitiveCompatibility>();
    public DbSet<OptimizationRun> OptimizationRuns => Set<OptimizationRun>();
    public DbSet<DatasetRecord> Datasets => Set<DatasetRecord>();

    // ── Operational sets ──────────────────────────────────────────────────────
    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<Degree> Degrees => Set<Degree>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<EducationalProgram> EducationalPrograms => Set<EducationalProgram>();
    public DbSet<AcademicCalendar> AcademicCalendars => Set<AcademicCalendar>();
    public DbSet<AcademicCalendarTerm> AcademicCalendarTerms => Set<AcademicCalendarTerm>();
    public DbSet<CurriculumPlan> CurriculumPlans => Set<CurriculumPlan>();
    public DbSet<CurriculumPlanItem> CurriculumPlanItems => Set<CurriculumPlanItem>();
    public DbSet<CurriculumPlanItemEdge> CurriculumPlanItemEdges => Set<CurriculumPlanItemEdge>();
    public DbSet<LearningAssignment> LearningAssignments => Set<LearningAssignment>();
    public DbSet<LearningAssignmentGroup> LearningAssignmentGroups => Set<LearningAssignmentGroup>();
    public DbSet<LearningAssignmentInstructor> LearningAssignmentInstructors => Set<LearningAssignmentInstructor>();
    public DbSet<LearningAssignmentPlanItem> LearningAssignmentPlanItems => Set<LearningAssignmentPlanItem>();

    // ── Anthropocentric sets ──────────────────────────────────────────────────
    public DbSet<HealthLimitation> HealthLimitations => Set<HealthLimitation>();
    public DbSet<InstructorConstraint> InstructorConstraints => Set<InstructorConstraint>();
    public DbSet<GroupConstraint> GroupConstraints => Set<GroupConstraint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Original entities
        modelBuilder.Entity<AcademicGroup>().HasKey(e => e.Id);
        modelBuilder.Entity<Instructor>().HasKey(e => e.Id);
        modelBuilder.Entity<Discipline>().HasKey(e => e.Id);
        modelBuilder.Entity<Room>().HasKey(e => e.Id);
        modelBuilder.Entity<TeachingAssignment>().HasKey(e => e.Id);
        modelBuilder.Entity<CognitiveCompatibility>().HasKey(e => e.Id);
        modelBuilder.Entity<OptimizationRun>().HasKey(e => e.Id);
        modelBuilder.Entity<DatasetRecord>().HasKey(e => e.Id);

        // Ignore navigation lists on AcademicGroup / Instructor (stored as Guid lists — not EF navigations)
        modelBuilder.Entity<AcademicGroup>().Ignore(e => e.HealthLimitationIds);
        modelBuilder.Entity<AcademicGroup>().Ignore(e => e.GroupConstraintIds);
        modelBuilder.Entity<Instructor>().Ignore(e => e.HealthLimitationIds);
        modelBuilder.Entity<Instructor>().Ignore(e => e.InstructorConstraintIds);

        // New operational entities
        modelBuilder.Entity<AcademicYear>().HasKey(e => e.Id);
        modelBuilder.Entity<Degree>().HasKey(e => e.Id);
        modelBuilder.Entity<Department>().HasKey(e => e.Id);
        modelBuilder.Entity<EducationalProgram>().HasKey(e => e.Id);
        modelBuilder.Entity<AcademicCalendar>().HasKey(e => e.Id);
        modelBuilder.Entity<AcademicCalendarTerm>().HasKey(e => e.Id);
        modelBuilder.Entity<CurriculumPlan>().HasKey(e => e.Id);
        modelBuilder.Entity<CurriculumPlanItem>().HasKey(e => e.Id);
        modelBuilder.Entity<CurriculumPlanItemEdge>().HasKey(e => e.Id);
        modelBuilder.Entity<LearningAssignment>().HasKey(e => e.Id);
        modelBuilder.Entity<LearningAssignmentGroup>().HasKey(e => new { e.LearningAssignmentId, e.GroupId });
        modelBuilder.Entity<LearningAssignmentInstructor>().HasKey(e => new { e.LearningAssignmentId, e.InstructorId });
        modelBuilder.Entity<LearningAssignmentPlanItem>().HasKey(e => new { e.LearningAssignmentId, e.CurriculumPlanItemId });

        // Anthropocentric entities
        modelBuilder.Entity<HealthLimitation>().HasKey(e => e.Id);
        modelBuilder.Entity<InstructorConstraint>().HasKey(e => e.Id);
        modelBuilder.Entity<GroupConstraint>().HasKey(e => e.Id);

        // ScheduledClass new fields — ignore list properties (not mapped to EF columns)
        modelBuilder.Entity<ScheduledClass>().Ignore(e => e.GroupIds);
        modelBuilder.Entity<ScheduledClass>().Ignore(e => e.InstructorIds);
    }
}



