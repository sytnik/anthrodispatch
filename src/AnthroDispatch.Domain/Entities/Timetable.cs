using AnthroDispatch.Domain.Metrics;

namespace AnthroDispatch.Domain.Entities;

public sealed class Timetable
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public List<ScheduledClass> Classes { get; init; } = [];
    public TimetableMetrics? Metrics { get; set; }

    public Timetable DeepClone()
    {
        var clone = new Timetable { Id = Guid.NewGuid() };
        foreach (var sc in Classes)
        {
            clone.Classes.Add(new ScheduledClass
            {
                Id = sc.Id,
                AssignmentId = sc.AssignmentId,
                GroupId = sc.GroupId,
                InstructorId = sc.InstructorId,
                DisciplineId = sc.DisciplineId,
                RoomId = sc.RoomId,
                Slot = sc.Slot,
                AtomicUnitId = sc.AtomicUnitId,
                SourceLearningAssignmentId = sc.SourceLearningAssignmentId,
                GroupIds = sc.GroupIds,
                InstructorIds = sc.InstructorIds,
                LessonType = sc.LessonType,
                EducationForm = sc.EducationForm
            });
        }

        if (Metrics != null)
        {
            clone.Metrics = new TimetableMetrics
            {
                FTech = Metrics.FTech, FCirc = Metrics.FCirc,
                FPsych = Metrics.FPsych, FCogn = Metrics.FCogn,
                CInterf = Metrics.CInterf,
                F = Metrics.F, Conflicts = Metrics.Conflicts,
                Satisfaction = Metrics.Satisfaction
            };
        }

        return clone;
    }
}