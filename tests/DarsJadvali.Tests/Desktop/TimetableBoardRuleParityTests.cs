using DarsJadvali.Application.Validation;
using DarsJadvali.Desktop.Models;
using DarsJadvali.Desktop.Services.Timetable;
using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;
using Xunit;

namespace DarsJadvali.Tests.Desktop;

/// <summary>
/// <b>Takrorlash yo'qolganini isbotlovchi test.</b>
/// </summary>
/// <remarks>
/// <para>
/// Ilgari <c>TimetableBoard.Evaluate</c> ish kunlari, kunlik dars chegarasi va ayniqsa
/// o'qituvchining ish vaqti qoidasini <b>o'zida qaytadan</b> hisoblardi, chunki
/// <c>ScheduleSnapshot</c> va <c>LessonAvailabilityRules</c> <c>internal</c> edi.
/// Ikkita implementatsiya bir-biridan ajralib ketishi mumkin edi.
/// </para>
/// <para>
/// Endi qoidalar <see cref="TimetableRuleSet.FromSnapshot"/> orqali AYNAN Application
/// nusxasidan olinadi. Quyidagi testlar butun (kun × soat) to'rini aylanib chiqib,
/// har bir katakda <c>TimetableBoard.Evaluate</c> va
/// <see cref="ScheduleValidator.Evaluate"/> <b>bir xil</b> qaror berishini tekshiradi.
/// </para>
/// </remarks>
public sealed class TimetableBoardRuleParityTests
{
    /// <summary>Snapshot yozuvlaridan taxta kartalarini yasaydi (bir yozuv = bir karta).</summary>
    private static List<TimetableCard> CardsFrom(ScheduleSnapshot snapshot)
    {
        var cards = new List<TimetableCard>();

        foreach (var entry in snapshot.Entries)
        {
            cards.Add(new TimetableCard
            {
                Id = entry.Id,
                EntityId = entry.Id,
                ClassGroupId = entry.ClassGroupId,
                ClassIds = new[] { entry.ClassGroupId },
                SubjectId = entry.SubjectId,
                TeacherIds = new[] { entry.TeacherId },
                SubjectName = snapshot.SubjectName(entry.SubjectId),
                TeacherNames = new[] { snapshot.TeacherName(entry.TeacherId) },
                ClassName = snapshot.ClassName(entry.ClassGroupId),
                Day = entry.DayOfWeek,
                Period = entry.LessonNumber,
                Length = 1,
                RoomNumber = entry.RoomNumber,
            });
        }

        return cards;
    }

    private static ScheduleEntryDraft DraftFor(TimetableCard card, WeekDay day, int period)
        => new(
            card.EntityId,
            card.ClassGroupId,
            card.SubjectId,
            card.TeacherIds[0],
            day,
            period,
            card.RoomNumber);

    /// <summary>To'rning har bir katagida ikki baholash bir xil qaror beradimi.</summary>
    private static void AssertParity(
        TimetableBoard board, ScheduleSnapshot snapshot, TimetableCard card, int maxPeriod)
    {
        var mismatches = new List<string>();

        foreach (var day in WeekDayExtensions.All)
        {
            for (var period = 1; period <= maxPeriod; period++)
            {
                var byBoard = board.Evaluate(card, day, period).IsAllowed;
                var byApplication = ScheduleValidator
                    .Evaluate(DraftFor(card, day, period), snapshot)
                    .IsValid;

                if (byBoard != byApplication)
                {
                    mismatches.Add($"{day.ToUzbek()} {period}-soat: board={byBoard}, application={byApplication}");
                }
            }
        }

        Assert.True(mismatches.Count == 0, string.Join("; ", mismatches));
    }

    [Fact]
    public async Task Kun_va_soat_chegarasi_Application_bilan_bir_xil_baholanadi()
    {
        using var db = new TestDbFactory();
        db.SeedDefaults(maxLessons: 7);

        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group, weeklyHours: 30);
        db.EnsureActiveSchedule();

        var snapshot = await db.Get<IScheduleSnapshotProvider>().LoadAsync();
        var rules = TimetableRuleSet.FromSnapshot(snapshot);

        var board = new TimetableBoard();
        var card = new TimetableCard
        {
            Id = 1,
            ClassGroupId = group.Id,
            ClassIds = new[] { group.Id },
            SubjectId = subject.Id,
            TeacherIds = new[] { teacher.Id },
            SubjectName = subject.Name,
            TeacherNames = new[] { teacher.FullName },
            ClassName = group.Name,
            Length = 1,
        };

        board.Load(new[] { card }, rules);

        // Yakshanba nofaol, 8-soat chegaradan tashqarida — ikkalasi ham shuni aytishi kerak.
        AssertParity(board, snapshot, card, maxPeriod: 9);

        Assert.False(board.Evaluate(card, WeekDay.Yakshanba, 1).IsAllowed);
        Assert.False(board.Evaluate(card, WeekDay.Dushanba, 8).IsAllowed);
        Assert.True(board.Evaluate(card, WeekDay.Dushanba, 7).IsAllowed);
    }

    [Fact]
    public async Task Oqituvchi_ish_vaqti_Application_qoidasidan_olinadi()
    {
        using var db = new TestDbFactory();
        db.SeedDefaults(maxLessons: 7);

        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group, weeklyHours: 30);
        db.EnsureActiveSchedule();

        // Dushanba kuni faqat 08:30–10:30 ishlaydi — oq ro'yxat (qolgan soatlar taqiqlanadi).
        db.AddAvailability(teacher, WeekDay.Dushanba, new TimeSpan(8, 30, 0), new TimeSpan(10, 30, 0));

        var snapshot = await db.Get<IScheduleSnapshotProvider>().LoadAsync();
        var rules = TimetableRuleSet.FromSnapshot(snapshot);

        var board = new TimetableBoard();
        var card = new TimetableCard
        {
            Id = 1,
            ClassGroupId = group.Id,
            ClassIds = new[] { group.Id },
            SubjectId = subject.Id,
            TeacherIds = new[] { teacher.Id },
            SubjectName = subject.Name,
            TeacherNames = new[] { teacher.FullName },
            ClassName = group.Name,
            Length = 1,
        };

        board.Load(new[] { card }, rules);

        AssertParity(board, snapshot, card, maxPeriod: 7);

        // Cheklov haqiqatan ishlayotganini ham tekshiramiz (parity "hammasi taqiq" bo'lib qolmasin).
        Assert.True(board.Evaluate(card, WeekDay.Dushanba, 1).IsAllowed);
        Assert.False(board.Evaluate(card, WeekDay.Dushanba, 5).IsAllowed);
        Assert.True(board.Evaluate(card, WeekDay.Seshanba, 5).IsAllowed);
    }

    [Fact]
    public async Task Bandlik_toqnashuvi_Application_bilan_bir_xil_baholanadi()
    {
        using var db = new TestDbFactory();
        db.SeedDefaults(maxLessons: 7);

        var teacher = db.AddTeacher();
        var other = db.AddTeacher("Karimova Nodira");
        var subject = db.AddSubject();
        var second = db.AddSubject("Fizika");
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group, weeklyHours: 30);
        db.AddAssignment(other, second, group, weeklyHours: 30);
        db.EnsureActiveSchedule();

        // Sinf Dushanba 3-soatda band.
        db.AddEntry(group, subject, teacher, WeekDay.Dushanba, 3);

        var snapshot = await db.Get<IScheduleSnapshotProvider>().LoadAsync();
        var rules = TimetableRuleSet.FromSnapshot(snapshot);

        var board = new TimetableBoard();
        var cards = CardsFrom(snapshot);

        var moving = new TimetableCard
        {
            Id = 9999,
            ClassGroupId = group.Id,
            ClassIds = new[] { group.Id },
            SubjectId = second.Id,
            TeacherIds = new[] { other.Id },
            SubjectName = second.Name,
            TeacherNames = new[] { other.FullName },
            ClassName = group.Name,
            Length = 1,
        };

        cards.Add(moving);
        board.Load(cards, rules);

        AssertParity(board, snapshot, moving, maxPeriod: 7);

        Assert.False(board.Evaluate(moving, WeekDay.Dushanba, 3).IsAllowed);
        Assert.True(board.Evaluate(moving, WeekDay.Dushanba, 4).IsAllowed);
    }

    [Fact]
    public async Task Nusxa_bir_marta_yuklanadi_va_baholash_bazaga_bormaydi()
    {
        using var db = new TestDbFactory();
        db.SeedDefaults(maxLessons: 12);

        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group, weeklyHours: 30);
        db.EnsureActiveSchedule();

        var snapshot = await db.Get<IScheduleSnapshotProvider>().LoadAsync();
        var rules = TimetableRuleSet.FromSnapshot(snapshot);

        var board = new TimetableBoard();
        var card = new TimetableCard
        {
            Id = 1,
            ClassGroupId = group.Id,
            ClassIds = new[] { group.Id },
            SubjectId = subject.Id,
            TeacherIds = new[] { teacher.Id },
            SubjectName = subject.Name,
            TeacherNames = new[] { teacher.FullName },
            ClassName = group.Name,
            Length = 1,
        };

        board.Load(new[] { card }, rules);

        // Bazani butunlay yopib qo'yamiz: baholash hali ham ishlashi kerak
        // (drag paytida bazaga umuman murojaat qilinmaydi).
        db.Dispose();

        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 1000; i++)
        {
            board.Evaluate(card, WeekDay.Dushanba, 1);
        }

        watch.Stop();

        var perCall = watch.Elapsed.TotalMilliseconds / 1000;
        Assert.True(perCall < 16, $"Bitta baholash {perCall:0.###} ms — 16 ms dan katta.");
        Assert.Equal(12, rules.MaxPeriodOf(WeekDay.Dushanba));
    }
}
