using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Application.Export;

/// <summary>Bitta katak ichidagi dars ma'lumoti.</summary>
/// <param name="SubjectName">Fan nomi.</param>
/// <param name="TeacherName">O'qituvchi (ko'rsatilmasa <c>null</c>).</param>
/// <param name="RoomNumber">Xona (ko'rsatilmasa <c>null</c>).</param>
public sealed record TimetableCellModel(string SubjectName, string? TeacherName, string? RoomNumber);

/// <summary>Jadvalning bitta qatori — bitta dars soati.</summary>
/// <param name="LessonNumber">Dars raqami (1..N).</param>
/// <param name="LessonLabel">Ko'rinadigan yozuv, masalan "3-soat".</param>
/// <param name="TimeLabel">Vaqt oralig'i, masalan "10:20-11:05". Sozlanmagan bo'lsa <c>null</c>.</param>
/// <param name="Cells">Har bir faol kun uchun katak (dars yo'q bo'lsa <c>null</c>).</param>
public sealed record TimetableRowModel(
    int LessonNumber,
    string LessonLabel,
    string? TimeLabel,
    IReadOnlyList<TimetableCellModel?> Cells);

/// <summary>Bitta sinfning jadval bloki — jadvalda N ta qator egallaydi.</summary>
/// <param name="ClassGroupId">Sinf Id'si.</param>
/// <param name="ClassName">Sinf nomi, masalan "5-A".</param>
/// <param name="Rows">Qatorlar.</param>
public sealed record TimetableClassBlockModel(
    int ClassGroupId,
    string ClassName,
    IReadOnlyList<TimetableRowModel> Rows);

/// <summary>Butun hujjat modeli — PDF chizuvchi faqat shunga tayanadi.</summary>
/// <param name="SchoolName">Maktab nomi (bo'lmasa <c>null</c>).</param>
/// <param name="Days">Faol ish kunlari, tartib bo'yicha.</param>
/// <param name="DayNames">Kun sarlavhalari (o'zbekcha), <paramref name="Days"/> bilan bir tartibda.</param>
/// <param name="Blocks">Sinf bloklari.</param>
/// <param name="EntryCount">Jadvaldagi jami dars soni.</param>
public sealed record TimetableDocumentModel(
    string? SchoolName,
    IReadOnlyList<WeekDay> Days,
    IReadOnlyList<string> DayNames,
    IReadOnlyList<TimetableClassBlockModel> Blocks,
    int EntryCount)
{
    /// <summary>Hech qanday dars yo'q (yoki umuman ko'rsatadigan narsa yo'q).</summary>
    public bool IsEmpty => EntryCount == 0 || Blocks.Count == 0 || Days.Count == 0;

    /// <summary>Bo'sh jadval uchun chiqadigan matn.</summary>
    public const string EmptyMessage = "Hali dars qo'yilmagan";
}
