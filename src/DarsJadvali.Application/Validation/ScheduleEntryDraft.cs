using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Application.Validation;

/// <summary>Tekshiriladigan (yoki joylashtiriladigan) dars yozuvi loyihasi.</summary>
/// <param name="Id">Mavjud yozuvni ko'chirayotganda uning Id'si, yangi bo'lsa null.</param>
/// <param name="ClassGroupId">Sinf Id.</param>
/// <param name="SubjectId">Fan Id.</param>
/// <param name="TeacherId">O'qituvchi Id.</param>
/// <param name="DayOfWeek">Hafta kuni.</param>
/// <param name="LessonNumber">Dars soati raqami.</param>
/// <param name="RoomNumber">Xona raqami.</param>
/// <param name="ScheduleId">
/// Qaysi dars jadvaliga (variantiga) tegishli. <c>null</c> bo'lsa faol jadval olinadi.
/// </param>
public sealed record ScheduleEntryDraft(
    int? Id,
    int ClassGroupId,
    int SubjectId,
    int TeacherId,
    WeekDay DayOfWeek,
    int LessonNumber,
    string? RoomNumber,
    int? ScheduleId = null);
