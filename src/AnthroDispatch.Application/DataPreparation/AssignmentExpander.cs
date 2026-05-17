using AnthroDispatch.Application.Abstractions;
using AnthroDispatch.Domain.Entities.Dispatch;
using AnthroDispatch.Domain.Entities.Operational;
using AnthroDispatch.Domain.Enums;

namespace AnthroDispatch.Application.DataPreparation;

/// <summary>
/// Expands LearningAssignments into AtomicSchedulingUnits.
/// </summary>
public sealed class AssignmentExpander(ICurriculumHoursCalculator calculator)
{
    private readonly ICurriculumHoursCalculator _calculator = calculator; // todo

    public List<AtomicSchedulingUnit> Expand(
        AnthroDispatchDataset dataset,
        IReadOnlyDictionary<Guid, AcademicCalendarTerm> termByCalendarPlanItem,
        int roomCapacityThreshold)
    {
        var units = new List<AtomicSchedulingUnit>();
        var roomDict = dataset.Rooms.ToDictionary(r => r.Id);
        var groupDict = dataset.Groups.ToDictionary(g => g.Id);

        foreach (var la in dataset.LearningAssignments)
        {
            var assignedGroupIds = dataset.LearningAssignmentGroups
                .Where(x => x.LearningAssignmentId == la.Id)
                .Select(x => x.GroupId)
                .ToList();

            var assignedInstructorIds = dataset.LearningAssignmentInstructors
                .Where(x => x.LearningAssignmentId == la.Id)
                .Select(x => x.InstructorId)
                .ToList();

            var planItemLinks = dataset.LearningAssignmentPlanItems
                .Where(x => x.LearningAssignmentId == la.Id)
                .ToList();

            if (assignedGroupIds.Count == 0 || planItemLinks.Count == 0) continue;

            // Determine required room type and education form
            var requiredRoomType = la.LessonType switch
            {
                LessonType.Laboratory => RoomType.Laboratory,
                LessonType.Online => RoomType.Online,
                LessonType.Lecture => RoomType.LectureHall,
                _ => RoomType.SeminarRoom
            };
            if (la.EducationForm == EducationForm.Distance) requiredRoomType = RoomType.Online;

            // Compute required periods
            var requiredPeriods = la.HoursFirstPart + la.HoursSecondPart > 0
                ? (int)Math.Ceiling((la.HoursFirstPart + la.HoursSecondPart) / 2.0)
                : 1;

            var disciplineId = planItemLinks.First().DisciplineId;
            var isOnline = la.EducationForm == EducationForm.Distance || requiredRoomType == RoomType.Online;

            // expansion rules
            if (la.LessonType == LessonType.Laboratory && !isOnline && assignedGroupIds.Count > 1)
            {
                // Split laboratory assignments by group unless a room supports the full set
                var totalStudents = assignedGroupIds.Sum(gid =>
                    groupDict.TryGetValue(gid, out var grp) ? grp.StudentCount : 0);
                var canMerge = dataset.Rooms.Any(r =>
                    r.Type == RoomType.Laboratory && r.Capacity >= totalStudents);

                if (!canMerge)
                {
                    foreach (var gid in assignedGroupIds)
                    {
                        units.Add(new AtomicSchedulingUnit
                        {
                            SourceLearningAssignmentId = la.Id,
                            DisciplineId = disciplineId,
                            GroupIds = [gid],
                            InstructorIds = assignedInstructorIds,
                            LessonType = la.LessonType,
                            RequiredRoomType = requiredRoomType,
                            EducationForm = la.EducationForm,
                            EducationLanguage = la.EducationLanguage,
                            RequiredPeriods = requiredPeriods,
                            Term = la.Term
                        });
                    }

                    continue;
                }
            }

            // Default: one shared unit
            units.Add(new AtomicSchedulingUnit
            {
                SourceLearningAssignmentId = la.Id,
                DisciplineId = disciplineId,
                GroupIds = assignedGroupIds,
                InstructorIds = assignedInstructorIds,
                LessonType = la.LessonType,
                RequiredRoomType = requiredRoomType,
                EducationForm = la.EducationForm,
                EducationLanguage = la.EducationLanguage,
                RequiredPeriods = requiredPeriods,
                Term = la.Term
            });
        }

        return units;
    }
}