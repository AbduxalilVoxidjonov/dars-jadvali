using System.Globalization;
using DarsJadvali.Application.Board;
using DarsJadvali.Application.Validation;
using DarsJadvali.Desktop.Models;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Desktop.Services.Timetable;

/// <summary>
/// <b>YAGONA</b> joy, qayerda UI karta modeli (<see cref="TimetableCard"/>) Application
/// qatlamining <c>Card</c>/<c>Lesson</c> o'qish modellari bilan uchrashadi.
/// </summary>
/// <remarks>
/// <para>
/// Bu fayl eski <c>ScheduleEntryCardAdapter</c> ning o'rniga keldi. Eski adapter
/// <c>ScheduleEntry</c> dan o'qigani uchun juft dars (<c>Length</c>), hafta maskasi
/// (<c>WeeksMask</c>), qulf (<c>IsLocked</c>) va guruh bo'linmasi (<c>GroupName</c>)
/// uchun <b>standart qiymat</b> berishga majbur edi — eski modelda bu ustunlar yo'q.
/// Endi to'rttasi ham <see cref="CardView"/> dan, ya'ni HAQIQIY manbadan keladi.
/// </para>
/// <para>
/// To'r, drag-drop va undo/redo entity'ni umuman ko'rmaydi — faqat
/// <see cref="TimetableCard"/> bilan ishlaydi, shuning uchun model almashuvi
/// shu bitta fayl bilan cheklandi.
/// </para>
/// </remarks>
public static class CardViewAdapter
{
    /// <summary>Standart karta rangi (o'qituvchi ham, fan ham rang bermasa).</summary>
    public const string DefaultColor = "#90A4AE";

    /// <summary>Jadvaldagi kartochkalarni UI kartalariga aylantiradi.</summary>
    /// <param name="views">Application'dan olingan kartochkalar.</param>
    /// <param name="teachers">O'qituvchilar (rang uchun).</param>
    /// <param name="subjects">Fanlar (rang uchun).</param>
    /// <param name="shiftOfClass">Sinf Id → smena raqami (ikki smena filtri uchun).</param>
    public static IReadOnlyList<TimetableCard> ToCards(
        IReadOnlyList<CardView> views,
        IReadOnlyList<Teacher> teachers,
        IReadOnlyList<Subject> subjects,
        IReadOnlyDictionary<int, int>? shiftOfClass = null)
    {
        ArgumentNullException.ThrowIfNull(views);
        ArgumentNullException.ThrowIfNull(teachers);
        ArgumentNullException.ThrowIfNull(subjects);

        var teacherColor = ColorMap(teachers.Select(t => (t.Id, t.ColorCode)));
        var subjectColor = ColorMap(subjects.Select(s => (s.Id, s.ColorCode)));

        var cards = new List<TimetableCard>(views.Count);

        foreach (var view in views)
        {
            cards.Add(new TimetableCard
            {
                // Kartochka Id UI ichida ham barqaror identifikator bo'lib xizmat qiladi.
                Id = view.CardId,
                EntityId = view.CardId,
                LessonId = view.LessonId,

                ClassGroupId = view.SchoolClassIds.Count > 0 ? view.SchoolClassIds[0] : 0,
                ClassIds = view.SchoolClassIds,
                GroupIds = view.StudentGroupIds,
                SubjectId = view.SubjectId,
                TeacherIds = view.TeacherIds,

                SubjectName = string.IsNullOrWhiteSpace(view.SubjectName) ? "(fan)" : view.SubjectName,
                TeacherNames = view.TeacherNames.Select(ShortName).ToList(),
                ClassName = string.IsNullOrWhiteSpace(view.ClassName) ? "(sinf)" : view.ClassName,

                // HAQIQIY guruh bo'linmasi — butun sinf darsida bo'sh satr.
                GroupName = view.GroupName ?? string.Empty,

                // Bir "dars" = Lesson. CTRL guruh ko'chishi shu bo'yicha ishlaydi.
                LessonKey = LessonKeyOf(view.LessonId),

                Day = DayNumbering.ToWeekDay(view.DayNo),
                Period = view.PeriodNo,

                // HAQIQIY juft dars uzunligi.
                Length = Math.Max(1, view.Length),

                // HAQIQIY hafta maskasi (A/B hafta).
                WeeksMask = view.WeeksMask <= 0 ? TimetableCard.AllWeeks : view.WeeksMask,

                // HAQIQIY qulf — bazadan keladi va dastur qayta ochilganda saqlanadi.
                IsLocked = view.IsLocked,

                ShiftNo = ShiftOf(shiftOfClass, view.SchoolClassIds),
                ColorCode = PickColor(teacherColor, subjectColor, view.TeacherIds, view.SubjectId),
                RoomNumber = view.RoomNumber,
            });
        }

        return cards;
    }

    /// <summary>
    /// To'liq joylashtirilmagan darslardan panel kartalarini yasaydi.
    /// </summary>
    /// <remarks>
    /// Ilgari bu ro'yxat "haftalik me'yor − qo'yilgan soat" bilan <b>taxmin</b> qilinardi.
    /// Endi u <see cref="ICardBoardService.GetUnplacedAsync"/> ning aniq natijasidan quriladi:
    /// son <c>Lesson.PeriodsPerWeek</c> va <c>SUM(Card.Length)</c> ayirmasidan olinadi.
    /// </remarks>
    /// <param name="lessons">Joylashtirilmagan darslar.</param>
    /// <param name="teachers">O'qituvchilar (rang uchun).</param>
    /// <param name="subjects">Fanlar (rang uchun).</param>
    /// <param name="startId">UI identifikatorlari shu qiymatdan boshlanadi.</param>
    /// <param name="classIdByName">
    /// Sinf nomi → <c>SchoolClass.Id</c>. <see cref="UnplacedLessonView"/> da sinf Id si
    /// yo'q (faqat nomi bor), shuning uchun to'qnashuv tekshiruvi ishlashi uchun Id shu
    /// lug'atdan tiklanadi — Application'ga <c>SchoolClassIds</c> qo'shilsa bu tushadi.
    /// </param>
    /// <param name="shiftOfClass">Sinf Id → smena raqami.</param>
    public static IReadOnlyList<TimetableCard> ToUnplacedCards(
        IReadOnlyList<UnplacedLessonView> lessons,
        IReadOnlyList<Teacher> teachers,
        IReadOnlyList<Subject> subjects,
        int startId,
        IReadOnlyDictionary<string, int>? classIdByName = null,
        IReadOnlyDictionary<int, int>? shiftOfClass = null)
    {
        ArgumentNullException.ThrowIfNull(lessons);
        ArgumentNullException.ThrowIfNull(teachers);
        ArgumentNullException.ThrowIfNull(subjects);

        var teacherColor = ColorMap(teachers.Select(t => (t.Id, t.ColorCode)));
        var subjectColor = ColorMap(subjects.Select(s => (s.Id, s.ColorCode)));

        var cards = new List<TimetableCard>();
        var nextId = startId;

        foreach (var lesson in lessons)
        {
            var remaining = lesson.RemainingPeriods;
            if (remaining <= 0)
            {
                continue;
            }

            // Juft dars istagi bo'lsa kartalar shu uzunlikda bo'linadi (qoldiq — bir soatlik).
            var perCard = Math.Max(1, lesson.PeriodsPerCard);

            var classId = 0;
            if (classIdByName is not null &&
                !string.IsNullOrWhiteSpace(lesson.ClassName) &&
                classIdByName.TryGetValue(lesson.ClassName, out var resolved))
            {
                classId = resolved;
            }

            while (remaining > 0)
            {
                var length = Math.Min(perCard, remaining);
                remaining -= length;

                cards.Add(new TimetableCard
                {
                    Id = nextId++,

                    // Bazada hali kartochka yo'q — saqlashda yangisi yaratiladi.
                    EntityId = null,
                    LessonId = lesson.LessonId,

                    ClassGroupId = classId,
                    ClassIds = classId > 0 ? new[] { classId } : Array.Empty<int>(),
                    SubjectId = lesson.SubjectId,
                    TeacherIds = lesson.TeacherIds,

                    SubjectName = string.IsNullOrWhiteSpace(lesson.SubjectName) ? "(fan)" : lesson.SubjectName,
                    TeacherNames = lesson.TeacherNames.Select(ShortName).ToList(),
                    ClassName = string.IsNullOrWhiteSpace(lesson.ClassName) ? "(sinf)" : lesson.ClassName,
                    GroupName = lesson.GroupName ?? string.Empty,
                    LessonKey = LessonKeyOf(lesson.LessonId),

                    Day = null,
                    Period = null,
                    Length = length,
                    WeeksMask = TimetableCard.AllWeeks,
                    IsLocked = false,

                    ShiftNo = classId > 0 && shiftOfClass is not null && shiftOfClass.TryGetValue(classId, out var s)
                        ? s
                        : 0,
                    ColorCode = PickColor(teacherColor, subjectColor, lesson.TeacherIds, lesson.SubjectId),
                });
            }
        }

        return cards;
    }

    /// <summary>
    /// Baholash qoidalarini <b>Application nusxasidan</b> quradi — qoida takrorlanmaydi.
    /// </summary>
    /// <param name="snapshot">Bir marta yuklangan jadval nusxasi.</param>
    /// <param name="periodNumbers">To'rda ko'rinadigan dars soati raqamlari (ikki smena — 1..12).</param>
    /// <param name="blockedTeacherSlots">
    /// O'qituvchi ishlamaydigan (o'qituvchi, kun, soat) uchliklari — ommaviy so'rovdan.
    /// </param>
    public static TimetableRuleSet ToRuleSet(
        ScheduleSnapshot snapshot,
        IReadOnlyList<int> periodNumbers,
        IEnumerable<(int TeacherId, WeekDay Day, int Period)>? blockedTeacherSlots = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(periodNumbers);

        return TimetableRuleSet.FromSnapshot(snapshot, periodNumbers, blockedTeacherSlots);
    }

    /// <summary>
    /// Kartani ko'chirish so'roviga aylantiradi.
    /// </summary>
    /// <remarks>
    /// Karta joylashtirilmagan bo'lsa, bazada kartochkasi bo'lmasa yoki dars soati
    /// raqami noma'lum bo'lsa <c>null</c> qaytadi — bunday karta <c>PlaceManyAsync</c>
    /// bilan ko'chirilmaydi (u yangi kartochka yaratishni talab qiladi).
    /// </remarks>
    /// <param name="card">Karta.</param>
    /// <param name="periodIdByNumber">Dars soati raqami → <c>Period.Id</c>.</param>
    public static CardPlacement? ToPlacement(
        TimetableCard card, IReadOnlyDictionary<int, int> periodIdByNumber)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(periodIdByNumber);

        if (!card.IsPlaced || card.EntityId is not { } cardId)
        {
            return null;
        }

        if (!periodIdByNumber.TryGetValue(card.Period!.Value, out var periodId))
        {
            return null;
        }

        return new CardPlacement(
            cardId,
            DayNumbering.ToDayNo(card.Day!.Value),
            periodId,
            card.WeeksMask);
    }

    /// <summary>Bir "dars"ni ifodalovchi kalit — CTRL guruh ko'chishida ishlatiladi.</summary>
    public static string LessonKeyOf(int lessonId)
        => string.Create(CultureInfo.InvariantCulture, $"L{lessonId}");

    /// <summary>"Voxidjonov Abduxalil" → "Voxidjonov A." ko'rinishidagi qisqartma.</summary>
    public static string ShortName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return "(o'qituvchi)";
        }

        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length < 2)
        {
            return parts.Length == 1 ? parts[0] : fullName.Trim();
        }

        var initials = parts
            .Skip(1)
            .Take(2)
            .Select(p => char.ToUpper(p[0], CultureInfo.CurrentCulture) + ".");

        return parts[0] + " " + string.Join(" ", initials);
    }

    private static Dictionary<int, string> ColorMap(IEnumerable<(int Id, string Color)> items)
    {
        var map = new Dictionary<int, string>();

        foreach (var (id, color) in items)
        {
            map[id] = color;
        }

        return map;
    }

    private static string PickColor(
        IReadOnlyDictionary<int, string> teacherColor,
        IReadOnlyDictionary<int, string> subjectColor,
        IReadOnlyList<int> teacherIds,
        int subjectId)
    {
        if (teacherIds.Count > 0 &&
            teacherColor.TryGetValue(teacherIds[0], out var byTeacher) &&
            !string.IsNullOrWhiteSpace(byTeacher))
        {
            return byTeacher;
        }

        return subjectColor.TryGetValue(subjectId, out var bySubject) && !string.IsNullOrWhiteSpace(bySubject)
            ? bySubject
            : DefaultColor;
    }

    private static int ShiftOf(IReadOnlyDictionary<int, int>? shiftOfClass, IReadOnlyList<int> classIds)
    {
        if (shiftOfClass is null)
        {
            return 0;
        }

        foreach (var id in classIds)
        {
            if (shiftOfClass.TryGetValue(id, out var shift))
            {
                return shift;
            }
        }

        return 0;
    }
}
