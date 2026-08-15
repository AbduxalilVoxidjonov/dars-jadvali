using System.Globalization;
using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Import;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;
using DarsJadvali.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DarsJadvali.Infrastructure.Import.Xml;

/// <summary>
/// Bitta import chaqiruvining holati va mantiqi. Har chaqiruvda YANGI nusxa yaratiladi.
/// </summary>
/// <remarks>
/// Sessiya <b>o'z tranzaksiyasini ochmaydi</b> (00 §6.4): uni <see cref="AscXmlImporter"/>
/// ochadi, sessiya esa faqat ichkarida ishlaydi.
/// </remarks>
internal sealed partial class AscImportSession
{
    /// <summary>Bir xil kodli ogohlantirishlarning hisobotga tushadigan maksimal soni.</summary>
    private const int MaxMessagesPerCode = 40;

    private static readonly string[] RomanNumerals =
    {
        "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII"
    };

    /// <summary>"Bo'sh o'rin" (vakansiya) o'qituvchisini aniqlash uchun boshlanmalar.</summary>
    private static readonly string[] VacancyPrefixes = { "вакант", "vakant", "vacant", "bo'sh", "bosh o" };

    private readonly AppDbContext _db;
    private readonly ICardOccurrenceProjector _projector;
    private readonly AscDocument _doc;
    private readonly ImportOptions _options;

    private readonly List<ImportMessage> _messages = new();
    private readonly Dictionary<string, int> _messageCountByCode = new(StringComparer.Ordinal);
    private readonly Dictionary<ImportEntityKind, Counter> _stats = new();

    // aSc id → bizning entity
    private readonly Dictionary<string, Subject> _subjectByAscId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Teacher> _teacherByAscId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Classroom> _classroomByAscId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Grade> _gradeByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SchoolClass> _classByAscId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StudentGroup> _groupByAscId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Lesson> _lessonByAscId = new(StringComparer.Ordinal);
    private readonly Dictionary<int, Period> _periodByNo = new();

    /// <summary>Sinf Id → o'sha sinfning BARCHA guruhlari (bandlikni yoyish uchun).</summary>
    private readonly Dictionary<int, List<StudentGroup>> _groupsByClassId = new();

    /// <summary>Guruh Id → guruhning o'zi (bandlikni yoyishda tez qidirish uchun).</summary>
    private readonly Dictionary<int, StudentGroup> _groupById = new();

    /// <summary>Sinfning "Butun sinf" guruhi.</summary>
    private readonly Dictionary<int, StudentGroup> _entireClassGroupByClassId = new();

    /// <summary>
    /// Dars Id → o'qituvchi Id lari. <c>DbSet.Local</c> o'rniga ataylab o'z indeksimiz:
    /// kontekstda import bilan bog'liq bo'lmagan, eskirgan bog'lanishlar bo'lishi mumkin.
    /// </summary>
    private readonly Dictionary<int, List<int>> _teachersByLessonId = new();

    /// <summary>Dars Id → guruh Id lari.</summary>
    private readonly Dictionary<int, List<int>> _groupsByLessonId = new();

    /// <summary>Chorak tartib raqami (0-based) → jadval varianti.</summary>
    private readonly List<Schedule> _schedules = new();

    private readonly List<Term> _terms = new();

    private AcademicYear _year = null!;
    private int _daysPerWeek = 6;
    private int _weeksInCycle = 1;
    private int _termsCount;

    public AscImportSession(
        AppDbContext db,
        ICardOccurrenceProjector projector,
        AscDocument doc,
        ImportOptions options)
    {
        _db = db;
        _projector = projector;
        _doc = doc;
        _options = options;
    }

    // -------------------------------------------------------------------------
    // Asosiy oqim
    // -------------------------------------------------------------------------

    /// <summary>Butun importni bajaradi. Chaqiruvchining tranzaksiyasi ichida ishlaydi.</summary>
    public async Task<ImportResult> RunAsync(CancellationToken ct)
    {
        var year = await _db.AcademicYears
            .FirstOrDefaultAsync(y => y.Id == _options.AcademicYearId, ct)
            .ConfigureAwait(false);

        if (year is null)
        {
            AddMessage(ImportSeverity.Error, "ASC-NO-YEAR",
                $"Maqsad o'quv yili topilmadi (Id = {_options.AcademicYearId}). Import bajarilmadi.");
            return BuildResult(false);
        }

        _year = year;

        DetectDimensions();

        if (_options.MergeMode == ImportMergeMode.Replace)
        {
            await ClearYearLessonsAsync(ct).ConfigureAwait(false);
        }

        await ImportPeriodsAsync(ct).ConfigureAwait(false);
        await ImportTermsAsync(ct).ConfigureAwait(false);
        await ImportSchedulesAsync(ct).ConfigureAwait(false);
        await ImportGradesAsync(ct).ConfigureAwait(false);
        await ImportSubjectsAsync(ct).ConfigureAwait(false);
        await ImportTeachersAsync(ct).ConfigureAwait(false);
        await ImportClassroomsAsync(ct).ConfigureAwait(false);
        await ImportClassesAsync(ct).ConfigureAwait(false);
        await ImportGroupsAsync(ct).ConfigureAwait(false);
        await ImportLessonsAsync(ct).ConfigureAwait(false);

        if (_options.ImportCards)
        {
            await ImportCardsAsync(ct).ConfigureAwait(false);
        }
        else
        {
            Stat(ImportEntityKind.Card).Found += _doc.Cards.Count;
            Stat(ImportEntityKind.Card).Skipped += _doc.Cards.Count;
            AddMessage(ImportSeverity.Info, "ASC-CARDS-OFF",
                $"Kartochkalar import qilinmadi (parametr bo'yicha): {_doc.Cards.Count} ta o'tkazib yuborildi.");
        }

        ReportUnsupported();

        await RebuildOccurrencesAsync(ct).ConfigureAwait(false);
        await ActivateScheduleAsync(ct).ConfigureAwait(false);

        return BuildResult(_messages.All(m => m.Severity != ImportSeverity.Error));
    }

    // -------------------------------------------------------------------------
    // O'lchovlar: kunlar / haftalar / choraklar
    // -------------------------------------------------------------------------

    /// <summary>
    /// XML'dan hafta kunlari, hafta sikli va choraklar sonini aniqlaydi va o'quv yilining
    /// o'lchovlarini FAQAT KATTALASHTIRADI.
    /// </summary>
    /// <remarks>
    /// Kichraytirish ataylab qilinmaydi: mavjud jadvalda 6-kunda darslar bo'lsa,
    /// 5 kunlik aSc eksporti ularni ko'rinmas qilib qo'yardi.
    /// </remarks>
    private void DetectDimensions()
    {
        var detectedDays = Math.Clamp(_doc.DetectedDaysPerWeek, 0, 14);
        var detectedWeeks = Math.Clamp(_doc.DetectedWeeksInCycle, 0, 12);
        var detectedTerms = Math.Clamp(_doc.DetectedTermsCount, 0, 12);

        _daysPerWeek = Math.Max(_year.DaysPerWeek, detectedDays > 0 ? detectedDays : 0);
        if (_daysPerWeek <= 0) _daysPerWeek = 6;

        _weeksInCycle = Math.Max(1, Math.Max(_year.WeeksInCycle, detectedWeeks));
        _termsCount = detectedTerms;

        if (_year.DaysPerWeek != _daysPerWeek)
        {
            AddMessage(ImportSeverity.Info, "ASC-DIM-DAYS",
                $"Hafta kunlari soni {_year.DaysPerWeek} dan {_daysPerWeek} ga oshirildi.");
            _year.DaysPerWeek = _daysPerWeek;
        }

        if (_year.WeeksInCycle != _weeksInCycle)
        {
            AddMessage(ImportSeverity.Info, "ASC-DIM-WEEKS",
                $"Hafta sikli {_year.WeeksInCycle} dan {_weeksInCycle} ga oshirildi (A/B hafta).");
            _year.WeeksInCycle = _weeksInCycle;
        }

        if (_termsCount > 0 && _year.TermsCount < _termsCount)
        {
            AddMessage(ImportSeverity.Info, "ASC-DIM-TERMS",
                $"Choraklar soni {_year.TermsCount} dan {_termsCount} ga oshirildi.");
            _year.TermsCount = _termsCount;
        }
    }

    // -------------------------------------------------------------------------
    // Almashtirish rejimi
    // -------------------------------------------------------------------------

    /// <summary>
    /// <see cref="ImportMergeMode.Replace"/> rejimida maqsad yilning butun
    /// "reja + jadval" qatlamini o'chiradi.
    /// </summary>
    /// <remarks>
    /// Kaskad FK'ga TAYANILMAYDI: testlardagi SQLite ulanishida <c>foreign_keys</c>
    /// pragmasi yoqilmagan bo'lishi mumkin. Shu sababli o'chirish tartibi qo'lda,
    /// bola jadvaldan ota jadvalga qarab bajariladi.
    /// </remarks>
    private async Task ClearYearLessonsAsync(CancellationToken ct)
    {
        var yearId = _year.Id;

        var cardIds = await _db.Cards
            .Where(c => _db.Lessons.Any(l => l.Id == c.LessonId && l.AcademicYearId == yearId))
            .Select(c => c.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var lessonIds = await _db.Lessons
            .Where(l => l.AcademicYearId == yearId)
            .Select(l => l.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (cardIds.Count > 0)
        {
            await _db.CardOccurrences.Where(o => cardIds.Contains(o.CardId)).ExecuteDeleteAsync(ct).ConfigureAwait(false);
            await _db.CardClassrooms.Where(cc => cardIds.Contains(cc.CardId)).ExecuteDeleteAsync(ct).ConfigureAwait(false);
            await _db.Cards.Where(c => cardIds.Contains(c.Id)).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        }

        if (lessonIds.Count > 0)
        {
            await _db.LessonTeachers.Where(x => lessonIds.Contains(x.LessonId)).ExecuteDeleteAsync(ct).ConfigureAwait(false);
            await _db.LessonClasses.Where(x => lessonIds.Contains(x.LessonId)).ExecuteDeleteAsync(ct).ConfigureAwait(false);
            await _db.LessonGroups.Where(x => lessonIds.Contains(x.LessonId)).ExecuteDeleteAsync(ct).ConfigureAwait(false);
            await _db.LessonClassrooms.Where(x => lessonIds.Contains(x.LessonId)).ExecuteDeleteAsync(ct).ConfigureAwait(false);
            await _db.Lessons.Where(l => lessonIds.Contains(l.Id)).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        }

        _db.ChangeTracker.Clear();

        AddMessage(ImportSeverity.Info, "ASC-REPLACE",
            $"Almashtirish rejimi: {lessonIds.Count} ta dars ta'rifi va {cardIds.Count} ta kartochka o'chirildi.");
    }

    // -------------------------------------------------------------------------
    // Dars soatlari
    // -------------------------------------------------------------------------

    /// <summary>
    /// aSc <c>periods</c> → <see cref="Period"/>. <c>PeriodNo</c> — o'quv yili ichida
    /// unikal va smenalar bo'ylab uzluksiz; aSc ro'yxati allaqachon shunday.
    /// </summary>
    private async Task ImportPeriodsAsync(CancellationToken ct)
    {
        var stat = Stat(ImportEntityKind.Period);

        var existing = await _db.Periods
            .Where(p => p.AcademicYearId == _year.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var period in existing) _periodByNo[period.PeriodNo] = period;

        foreach (var source in _doc.Periods.OrderBy(p => p.Number))
        {
            stat.Found++;

            if (source.Number < 0)
            {
                stat.Skipped++;
                AddMessage(ImportSeverity.Warning, "ASC-INVALID-VALUE",
                    $"Dars soati raqami manfiy ({source.Number}) — o'tkazib yuborildi.");
                continue;
            }

            var (start, end) = ResolveTimes(source);

            if (_periodByNo.TryGetValue(source.Number, out var entity))
            {
                entity.Name = Cut(source.Name, 50) ?? entity.Name;
                entity.ShortName = Cut(source.Short, 10) ?? entity.ShortName;
                entity.StartTime = start;
                entity.EndTime = end;
                stat.Updated++;
                continue;
            }

            entity = new Period
            {
                AcademicYearId = _year.Id,
                PeriodNo = source.Number,
                Name = Cut(source.Name, 50) ?? $"{source.Number}-dars",
                ShortName = Cut(source.Short, 10) ?? source.Number.ToString(CultureInfo.InvariantCulture),
                StartTime = start,
                EndTime = end
            };

            _db.Periods.Add(entity);
            _periodByNo[source.Number] = entity;
            stat.Created++;
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Vaqtlarni to'ldiradi. aSc eksportida <c>starttime</c>/<c>endtime</c> bo'sh bo'lishi
    /// mumkin, bizda esa <c>CK_Periods_TimeOrder</c> majburiy — shuning uchun
    /// 08:00 dan boshlab 45 daqiqa dars + 10 daqiqa tanaffus bo'yicha hisoblanadi.
    /// </summary>
    private static (TimeOnly Start, TimeOnly End) ResolveTimes(AscPeriod source)
    {
        var start = source.StartTime;
        var end = source.EndTime;

        if (start is null || end is null || end <= start)
        {
            var offset = 8 * 60 + Math.Max(0, source.Number - 1) * 55;
            if (source.Number == 0) offset = 7 * 60 + 5;
            offset = Math.Clamp(offset, 0, 23 * 60 + 14);

            start ??= new TimeOnly(offset / 60, offset % 60);
            end = start.Value.AddMinutes(45);

            // Yarim tundan oshib ketmasin.
            if (end <= start) end = new TimeOnly(23, 59);
        }

        return (start.Value, end.Value);
    }

    // -------------------------------------------------------------------------
    // Choraklar va jadval variantlari
    // -------------------------------------------------------------------------

    /// <summary>
    /// aSc <c>termsdefs</c> → <see cref="Term"/>. Bir bitli ta'riflar chorak nomini beradi.
    /// </summary>
    private async Task ImportTermsAsync(CancellationToken ct)
    {
        var stat = Stat(ImportEntityKind.Term);
        stat.Found += _doc.TermsDefs.Count;

        if (_termsCount <= 0) return;

        // Bir bitli termsdef → o'sha chorakning nomi.
        var names = new Dictionary<int, AscBitDef>();
        foreach (var def in _doc.TermsDefs)
        {
            var bits = AscBitmask.Bits(def.Mask).ToList();
            if (bits.Count == 1) names.TryAdd(bits[0], def);
        }

        var existing = await _db.Terms
            .Where(t => t.AcademicYearId == _year.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        for (var index = 0; index < _termsCount; index++)
        {
            var ordinal = index + 1;
            var roman = ordinal <= RomanNumerals.Length
                ? RomanNumerals[ordinal - 1]
                : ordinal.ToString(CultureInfo.InvariantCulture);

            names.TryGetValue(index, out var def);
            var name = Cut(def?.Name, 50) ?? $"{roman} chorak";
            var shortName = Cut(def?.Short, 10) ?? roman;

            var term = existing.FirstOrDefault(t => t.Ordinal == ordinal);
            if (term is null)
            {
                term = new Term
                {
                    AcademicYearId = _year.Id,
                    Ordinal = ordinal,
                    Name = name,
                    ShortName = shortName
                };
                _db.Terms.Add(term);
                stat.Created++;
            }
            else
            {
                term.Name = name;
                term.ShortName = shortName;
                stat.Updated++;
            }

            _terms.Add(term);
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Har chorak uchun ALOHIDA <see cref="Schedule"/> varianti (tasdiqlangan qaror:
    /// <c>TermsMask</c> ishlatilmaydi). Chorak umuman bo'lmasa — bitta variant.
    /// </summary>
    private async Task ImportSchedulesAsync(CancellationToken ct)
    {
        var stat = Stat(ImportEntityKind.Schedule);

        var prefix = UniqueNames.Normalize(_options.SchedulePrefix);
        if (prefix.Length == 0) prefix = "aSc import";

        var existing = await _db.Schedules
            .Where(s => s.AcademicYearId == _year.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var targets = new List<(Term? Term, string Name)>();
        if (_terms.Count > 0)
        {
            foreach (var term in _terms) targets.Add((term, $"{prefix} — {term.Name}"));
        }
        else
        {
            targets.Add((null, prefix));
        }

        foreach (var (term, rawName) in targets)
        {
            stat.Found++;
            var name = UniqueNames.Truncate(rawName, 100);

            var schedule = existing.FirstOrDefault(s =>
                string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

            if (schedule is null)
            {
                schedule = new Schedule
                {
                    AcademicYearId = _year.Id,
                    TermId = term?.Id,
                    Name = name,
                    IsActive = false,
                    WeeksInCycle = _weeksInCycle,
                    CreatedAt = DateTime.UtcNow,
                    Note = "aSc TimeTables XML importidan"
                };
                _db.Schedules.Add(schedule);
                stat.Created++;
            }
            else
            {
                schedule.TermId = term?.Id;
                schedule.WeeksInCycle = Math.Max(schedule.WeeksInCycle, _weeksInCycle);
                stat.Updated++;
            }

            _schedules.Add(schedule);
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // -------------------------------------------------------------------------
    // Parallellar
    // -------------------------------------------------------------------------

    private async Task ImportGradesAsync(CancellationToken ct)
    {
        var stat = Stat(ImportEntityKind.Grade);

        var existing = await _db.Grades
            .IgnoreQueryFilters()
            .Where(g => g.AcademicYearId == _year.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var byNo = existing.ToDictionary(g => g.GradeNo);

        foreach (var source in _doc.Grades)
        {
            stat.Found++;

            if (source.GradeNo is < 0 or > 20)
            {
                stat.Skipped++;
                AddMessage(ImportSeverity.Warning, "ASC-INVALID-VALUE",
                    $"Parallel raqami chegaradan tashqarida ({source.GradeNo}, ruxsat 0..20) — o'tkazib yuborildi.",
                    source.Id);
                continue;
            }

            var name = Cut(source.Name, 50) ?? $"{source.GradeNo}-parallel";
            var shortName = Cut(source.Short, 16) ?? source.GradeNo.ToString(CultureInfo.InvariantCulture);

            if (byNo.TryGetValue(source.GradeNo, out var grade))
            {
                grade.Name = name;
                grade.ShortName = shortName;
                if (grade.IsDeleted) grade.IsDeleted = false;
                stat.Updated++;
            }
            else
            {
                grade = new Grade
                {
                    AcademicYearId = _year.Id,
                    GradeNo = source.GradeNo,
                    Name = name,
                    ShortName = shortName
                };
                _db.Grades.Add(grade);
                byNo[source.GradeNo] = grade;
                stat.Created++;
            }

            // Sinf `grade` atributida 2012'da daraja soni, 2008'da grades.id turadi —
            // ikkala kalit ham qidiriladigan qilib yoziladi.
            _gradeByKey[source.GradeNo.ToString(CultureInfo.InvariantCulture)] = grade;
            if (!string.IsNullOrWhiteSpace(source.Id)) _gradeByKey[source.Id] = grade;
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // -------------------------------------------------------------------------
    // Fanlar
    // -------------------------------------------------------------------------

    private async Task ImportSubjectsAsync(CancellationToken ct)
    {
        var stat = Stat(ImportEntityKind.Subject);

        var existing = await _db.Subjects
            .IgnoreQueryFilters()
            .Where(s => s.AcademicYearId == _year.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // UX_Subjects_Code — GLOBAL unikal indeks, filtri yo'q: o'chirilgan fanlar ham
        // kodni band qiladi. Shuning uchun butun jadval bo'ylab o'qiladi.
        var allCodes = await _db.Subjects
            .IgnoreQueryFilters()
            .Select(s => s.Code)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var codes = new UniqueNames(30, allCodes);
        var shorts = new UniqueNames(24, existing.Where(s => !s.IsDeleted).Select(s => s.ShortName));

        var byExternal = BuildExternalIndex(existing, s => s.ExternalId);
        var byShort = existing
            .Where(s => !s.IsDeleted && !string.IsNullOrWhiteSpace(s.ShortName))
            .GroupBy(s => s.ShortName!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var source in _doc.Subjects)
        {
            stat.Found++;

            var name = Cut(source.Name, 150) ?? Cut(source.Short, 150) ?? source.Id;
            var shortCandidate = source.Short ?? source.Name ?? source.Id;

            var subject = Lookup(byExternal, byShort, source.Id, source.Short);

            if (subject is null)
            {
                subject = new Subject
                {
                    AcademicYearId = _year.Id,
                    Name = name,
                    ShortName = shorts.Take(shortCandidate, source.Id),
                    Code = codes.Take(shortCandidate, source.Id),
                    ExternalId = Cut(source.Id, 64)
                };
                _db.Subjects.Add(subject);
                stat.Created++;
            }
            else
            {
                shorts.Release(subject.ShortName);
                codes.Release(subject.Code);

                subject.AcademicYearId = _year.Id;
                subject.Name = name;
                subject.ShortName = shorts.Take(shortCandidate, source.Id);
                subject.Code = codes.Take(shortCandidate, source.Id);
                subject.ExternalId = Cut(source.Id, 64);
                if (subject.IsDeleted) subject.IsDeleted = false;
                stat.Updated++;
            }

            _subjectByAscId[source.Id] = subject;
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // -------------------------------------------------------------------------
    // O'qituvchilar
    // -------------------------------------------------------------------------

    private async Task ImportTeachersAsync(CancellationToken ct)
    {
        var stat = Stat(ImportEntityKind.Teacher);

        var existing = await _db.Teachers
            .IgnoreQueryFilters()
            .Where(t => t.AcademicYearId == _year.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var shorts = new UniqueNames(24, existing.Where(t => !t.IsDeleted).Select(t => t.ShortName));
        var byExternal = BuildExternalIndex(existing, t => t.ExternalId);
        var byShort = existing
            .Where(t => !t.IsDeleted && !string.IsNullOrWhiteSpace(t.ShortName))
            .GroupBy(t => t.ShortName!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var source in _doc.Teachers)
        {
            stat.Found++;

            var fullName = Cut(source.Name, 200)
                           ?? Cut(JoinName(source.FirstName, source.LastName), 200)
                           ?? Cut(source.Short, 200)
                           ?? source.Id;

            var teacher = Lookup(byExternal, byShort, source.Id, source.Short);

            if (teacher is null)
            {
                teacher = new Teacher
                {
                    AcademicYearId = _year.Id,
                    FullName = fullName,
                    ShortName = shorts.Take(source.Short ?? fullName, source.Id),
                    ExternalId = Cut(source.Id, 64)
                };
                _db.Teachers.Add(teacher);
                stat.Created++;
            }
            else
            {
                shorts.Release(teacher.ShortName);
                teacher.AcademicYearId = _year.Id;
                teacher.FullName = fullName;
                teacher.ShortName = shorts.Take(source.Short ?? fullName, source.Id);
                teacher.ExternalId = Cut(source.Id, 64);
                if (teacher.IsDeleted) teacher.IsDeleted = false;
                stat.Updated++;
            }

            teacher.FirstName = Cut(source.FirstName, 128);
            teacher.LastName = Cut(source.LastName, 128);
            teacher.Email = Cut(source.Email, 256);
            teacher.Phone = Cut(source.Mobile, 50);
            teacher.Gender = ParseGender(source.Gender);
            teacher.IsVacancy = IsVacancy(fullName);

            _teacherByAscId[source.Id] = teacher;
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static string? JoinName(string? first, string? last)
    {
        var joined = string.Join(' ', new[] { last, first }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return joined.Length == 0 ? null : joined;
    }

    private static Gender? ParseGender(string? raw) => raw?.Trim().ToUpperInvariant() switch
    {
        "M" or "1" or "MALE" => Gender.Male,
        "F" or "2" or "FEMALE" => Gender.Female,
        _ => null
    };

    private static bool IsVacancy(string fullName)
    {
        var normalized = fullName.Trim();
        return VacancyPrefixes.Any(p => normalized.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    // -------------------------------------------------------------------------
    // Xonalar
    // -------------------------------------------------------------------------

    private async Task ImportClassroomsAsync(CancellationToken ct)
    {
        var stat = Stat(ImportEntityKind.Classroom);

        var existing = await _db.Classrooms
            .IgnoreQueryFilters()
            .Where(c => c.AcademicYearId == _year.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var shorts = new UniqueNames(24, existing.Where(c => !c.IsDeleted).Select(c => c.ShortName));
        var byExternal = BuildExternalIndex(existing, c => c.ExternalId);
        var byShort = existing
            .Where(c => !c.IsDeleted)
            .GroupBy(c => c.ShortName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var source in _doc.Classrooms)
        {
            stat.Found++;

            var name = Cut(source.Name, 128) ?? Cut(source.Short, 128) ?? source.Id;

            // CK_Classrooms_Capacity: NULL yoki musbat. aSc'da 0 va -1 "ko'rsatilmagan".
            var capacity = source.Capacity is > 0 ? source.Capacity : null;

            var classroom = Lookup(byExternal, byShort, source.Id, source.Short);

            if (classroom is null)
            {
                classroom = new Classroom
                {
                    AcademicYearId = _year.Id,
                    Name = name,
                    ShortName = shorts.Take(source.Short ?? name, source.Id),
                    Capacity = capacity,
                    ExternalId = Cut(source.Id, 64)
                };
                _db.Classrooms.Add(classroom);
                stat.Created++;
            }
            else
            {
                shorts.Release(classroom.ShortName);
                classroom.AcademicYearId = _year.Id;
                classroom.Name = name;
                classroom.ShortName = shorts.Take(source.Short ?? name, source.Id);
                classroom.Capacity = capacity;
                classroom.ExternalId = Cut(source.Id, 64);
                if (classroom.IsDeleted) classroom.IsDeleted = false;
                stat.Updated++;
            }

            _classroomByAscId[source.Id] = classroom;
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // -------------------------------------------------------------------------
    // Umumiy yordamchilar
    // -------------------------------------------------------------------------

    /// <summary>
    /// <c>ExternalId</c> bo'yicha indeks. Bir xil <c>ExternalId</c> ikki marta uchrasa
    /// birinchisi olinadi (unikal indeks yo'q — bu himoya).
    /// </summary>
    private static Dictionary<string, T> BuildExternalIndex<T>(
        IEnumerable<T> items, Func<T, string?> externalId)
    {
        var map = new Dictionary<string, T>(StringComparer.Ordinal);

        foreach (var item in items)
        {
            var key = externalId(item);
            if (string.IsNullOrWhiteSpace(key)) continue;
            map.TryAdd(key, item);
        }

        return map;
    }

    /// <summary>
    /// Idempotentlikning yadrosi: avval <c>ExternalId</c> (aSc <c>id</c>) bo'yicha,
    /// so'ng tabiiy kalit (qisqartma) bo'yicha qidiriladi.
    /// </summary>
    private static T? Lookup<T>(
        Dictionary<string, T> byExternal,
        Dictionary<string, T> byNatural,
        string ascId,
        string? naturalKey) where T : class
    {
        if (byExternal.TryGetValue(ascId, out var found)) return found;

        if (!string.IsNullOrWhiteSpace(naturalKey)
            && byNatural.TryGetValue(naturalKey.Trim(), out var natural))
        {
            return natural;
        }

        return null;
    }

    /// <summary>Satrni tozalab kesadi; bo'sh bo'lsa <c>null</c>.</summary>
    private static string? Cut(string? value, int maxLength)
    {
        var normalized = UniqueNames.Normalize(value);
        return normalized.Length == 0 ? null : UniqueNames.Truncate(normalized, maxLength);
    }

    private Counter Stat(ImportEntityKind kind)
    {
        if (_stats.TryGetValue(kind, out var counter)) return counter;

        counter = new Counter();
        _stats[kind] = counter;
        return counter;
    }

    private void AddMessage(ImportSeverity severity, string code, string text, string? reference = null)
    {
        var count = _messageCountByCode.GetValueOrDefault(code);
        _messageCountByCode[code] = count + 1;

        if (severity != ImportSeverity.Error && count >= MaxMessagesPerCode) return;

        _messages.Add(new ImportMessage(severity, code, text, reference));
    }

    private void ReportUnsupported()
    {
        if (_doc.Students.Count > 0)
        {
            var stat = Stat(ImportEntityKind.Student);
            stat.Found += _doc.Students.Count;
            stat.Skipped += _doc.Students.Count;

            AddMessage(ImportSeverity.Warning, "ASC-STUDENTS-SKIPPED",
                $"O'quvchilar hozircha import qilinmaydi (P2): {_doc.Students.Count} ta yozuv o'tkazib yuborildi.");
        }

        foreach (var (section, count) in _doc.UnsupportedSections.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            AddMessage(ImportSeverity.Warning, "ASC-UNSUPPORTED",
                $"'{section}' bo'limi qo'llab-quvvatlanmaydi: {count} ta yozuv o'tkazib yuborildi.");
        }
    }

    /// <summary>Kartochkalar yozilgach bandlik jadvalini qayta quradi.</summary>
    private async Task RebuildOccurrencesAsync(CancellationToken ct)
    {
        foreach (var schedule in _schedules)
        {
            await _projector.RebuildForScheduleAsync(schedule.Id, ct).ConfigureAwait(false);
        }
    }

    private async Task ActivateScheduleAsync(CancellationToken ct)
    {
        if (!_options.ActivateFirstSchedule || _schedules.Count == 0) return;

        var target = _schedules[0];
        if (target.IsActive) return;

        // UX_Schedules_IsActive: ayni paytda faqat BITTA faol jadval bo'ladi.
        // Tartib muhim — avval hammasini o'chirib, keyin bittasini yoqamiz.
        var actives = await _db.Schedules.Where(s => s.IsActive).ToListAsync(ct).ConfigureAwait(false);
        foreach (var schedule in actives) schedule.IsActive = false;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        target.IsActive = true;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        AddMessage(ImportSeverity.Info, "ASC-ACTIVATED",
            $"'{target.Name}' jadval varianti faol qilindi.");
    }

    private ImportResult BuildResult(bool success)
    {
        // Bostirilgan takroriy ogohlantirishlar haqida yakuniy xabar.
        foreach (var (code, count) in _messageCountByCode.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (count <= MaxMessagesPerCode) continue;

            _messages.Add(new ImportMessage(ImportSeverity.Info, "ASC-TRUNCATED",
                $"'{code}' turidagi yana {count - MaxMessagesPerCode} ta xabar hisobotga sig'madi " +
                $"(jami {count} ta)."));
        }

        var stats = new List<ImportEntityStat>();
        foreach (var kind in Enum.GetValues<ImportEntityKind>())
        {
            var counter = _stats.TryGetValue(kind, out var found) ? found : new Counter();
            stats.Add(new ImportEntityStat(
                kind, Title(kind), counter.Found, counter.Created, counter.Updated, counter.Skipped));
        }

        return new ImportResult
        {
            Success = success,
            DryRun = _options.DryRun,
            AcademicYearId = _options.AcademicYearId,
            Stats = stats,
            Messages = _messages.ToList(),
            ScheduleIds = _schedules.Select(s => s.Id).ToList(),
            ScheduleNames = _schedules.Select(s => s.Name).ToList(),
            Source = new AscSourceSummary(
                _doc.FormatName,
                _doc.DetectedDaysPerWeek,
                _doc.DetectedWeeksInCycle,
                _doc.DetectedTermsCount,
                _doc.Periods.Count,
                _doc.Subjects.Count,
                _doc.Teachers.Count,
                _doc.Classrooms.Count,
                _doc.Grades.Count,
                _doc.Classes.Count,
                _doc.Groups.Count,
                _doc.Lessons.Count,
                _doc.Cards.Count,
                _doc.Students.Count)
        };
    }

    private static string Title(ImportEntityKind kind) => kind switch
    {
        ImportEntityKind.Period => "Dars soatlari",
        ImportEntityKind.Term => "Choraklar",
        ImportEntityKind.Schedule => "Jadval variantlari",
        ImportEntityKind.Grade => "Parallellar",
        ImportEntityKind.Subject => "Fanlar",
        ImportEntityKind.Teacher => "O'qituvchilar",
        ImportEntityKind.Classroom => "Xonalar",
        ImportEntityKind.SchoolClass => "Sinflar",
        ImportEntityKind.ClassDivision => "Bo'linishlar",
        ImportEntityKind.StudentGroup => "Guruhlar",
        ImportEntityKind.Lesson => "Darslar",
        ImportEntityKind.Card => "Kartochkalar",
        ImportEntityKind.Student => "O'quvchilar",
        _ => kind.ToString()
    };

    /// <summary>O'zgaruvchan sanoq — hisobot oxirida <see cref="ImportEntityStat"/> ga aylanadi.</summary>
    private sealed class Counter
    {
        public int Found;
        public int Created;
        public int Updated;
        public int Skipped;
    }
}
