using AnthroDispatch.Domain.Entities.Operational;
using AnthroDispatch.Domain.Enums;

namespace AnthroDispatch.Application.DataPreparation;

public sealed class CurriculumHoursCalculator : ICurriculumHoursCalculator
{
    public int CalculateRequiredPeriods(
        CurriculumPlanItem item,
        AcademicCalendarTerm term,
        LessonType lessonType,
        EducationForm educationForm)
    {
        int hours;
        if (educationForm == EducationForm.Distance)
        {
            // distance formula
            hours = item.LabWorkDistanceSessionHours + item.PracticalWorkDistanceSessionHours;
        }
        else
        {
            // full-time formula
            hours = lessonType switch
            {
                LessonType.Lecture => term.PartOneWeeks * item.LecturePerWeekFirst +
                                      term.PartTwoWeeks * item.LecturePerWeekSecond,
                LessonType.Laboratory => term.PartOneWeeks * item.LabWorkPerWeekFirst +
                                         term.PartTwoWeeks * item.LabWorkPerWeekSecond,
                LessonType.Practice => term.PartOneWeeks * item.PracticalWorkPerWeekFirst +
                                       term.PartTwoWeeks * item.PracticalWorkPerWeekSecond,
                _ => term.PartOneWeeks * item.LecturePerWeekFirst + term.PartTwoWeeks * item.LecturePerWeekSecond,
            };
        }

        // period conversion: 1 period = 2 academic hours
        return (int)Math.Ceiling(hours / 2.0);
    }
}