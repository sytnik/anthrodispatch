using AnthroDispatch.Domain.Entities;
using AnthroDispatch.Domain.Enums;

namespace AnthroDispatch.Application.Algorithms.Objective;

/// <summary>Extension helpers for ScheduledClass — online detection.</summary>
internal static class ScheduledClassExtensions
{
    public static bool IsOnline(this ScheduledClass sc)
        => sc.EducationForm == EducationForm.Distance ||
           sc.LessonType == LessonType.Online;
}