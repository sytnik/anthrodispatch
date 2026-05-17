using AnthroDispatch.Domain.Entities.Operational;
using AnthroDispatch.Domain.Enums;

namespace AnthroDispatch.Application.DataPreparation;

public interface ICurriculumHoursCalculator
{
    int CalculateRequiredPeriods(
        CurriculumPlanItem item,
        AcademicCalendarTerm term,
        LessonType lessonType,
        EducationForm educationForm);
}