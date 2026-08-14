using DarsJadvali.Application.Validation;
using DarsJadvali.Domain.Enums;
using Xunit;

namespace DarsJadvali.Tests;

/// <summary>
/// CONTRACT 2.2 dagi 10 ta qoidaning har biri uchun test.
/// Har bir testda: Arrange (ma'lumot tayyorlash) → Act (validatsiya) → Assert (kod tekshiruvi).
/// </summary>
public class ScheduleValidatorTests
{
    private static bool Has(ValidationResult result, string code)
        => result.Conflicts.Any(c => c.Code == code);

    // -----------------------------------------------------------------
    // 1. DAY_INACTIVE
    // -----------------------------------------------------------------
    [Fact]
    public async Task Nofaol_kunga_dars_qoyilsa_DAY_INACTIVE_xatosini_beradi()
    {
        // Arrange — Yakshanba seed'da nofaol kun.
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group);

        var draft = new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Yakshanba, 1, null);

        // Act
        var result = await db.Get<IScheduleValidator>().ValidateAsync(draft);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(Has(result, ConflictCodes.DayInactive));
    }

    // -----------------------------------------------------------------
    // 2. LESSON_OUT_OF_RANGE
    // -----------------------------------------------------------------
    [Fact]
    public async Task Dars_raqami_oralikdan_tashqarida_bolsa_LESSON_OUT_OF_RANGE_beradi()
    {
        // Arrange — kunlik maksimum 7 ta dars, biz 9-darsni so'raymiz.
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group);

        var draft = new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Dushanba, 9, null);

        // Act
        var result = await db.Get<IScheduleValidator>().ValidateAsync(draft);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(Has(result, ConflictCodes.LessonOutOfRange));
    }

    [Fact]
    public async Task Dars_raqami_noldan_kichik_bolsa_LESSON_OUT_OF_RANGE_beradi()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group);

        var draft = new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Dushanba, 0, null);

        // Act
        var result = await db.Get<IScheduleValidator>().ValidateAsync(draft);

        // Assert
        Assert.True(Has(result, ConflictCodes.LessonOutOfRange));
    }

    // -----------------------------------------------------------------
    // 3. TEACHER_INACTIVE
    // -----------------------------------------------------------------
    [Fact]
    public async Task Nofaol_oqituvchi_uchun_TEACHER_INACTIVE_beradi()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher("Nofaol O'qituvchi", isActive: false);
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group);

        var draft = new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Dushanba, 1, null);

        // Act
        var result = await db.Get<IScheduleValidator>().ValidateAsync(draft);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(Has(result, ConflictCodes.TeacherInactive));
    }

    // -----------------------------------------------------------------
    // 4. NO_ASSIGNMENT
    // -----------------------------------------------------------------
    [Fact]
    public async Task Biriktirma_bolmasa_NO_ASSIGNMENT_beradi()
    {
        // Arrange — ataylab TeacherAssignment yaratilmaydi.
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();

        var draft = new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Dushanba, 1, null);

        // Act
        var result = await db.Get<IScheduleValidator>().ValidateAsync(draft);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(Has(result, ConflictCodes.NoAssignment));
    }

    // -----------------------------------------------------------------
    // 5. TEACHER_BUSY
    // -----------------------------------------------------------------
    [Fact]
    public async Task Oqituvchi_shu_vaqtda_band_bolsa_TEACHER_BUSY_beradi()
    {
        // Arrange — bitta o'qituvchi, ikki xil sinf, ayni kun va soat.
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var groupA = db.AddClassGroup("5-A");
        var groupB = db.AddClassGroup("5-B");
        db.AddAssignment(teacher, subject, groupA);
        db.AddAssignment(teacher, subject, groupB);
        db.AddEntry(groupA, subject, teacher, WeekDay.Dushanba, 1);

        var draft = new ScheduleEntryDraft(null, groupB.Id, subject.Id, teacher.Id, WeekDay.Dushanba, 1, null);

        // Act
        var result = await db.Get<IScheduleValidator>().ValidateAsync(draft);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(Has(result, ConflictCodes.TeacherBusy));
    }

    /// <summary>
    /// Klassik xato: mavjud yozuvni o'z joyiga "ko'chirganda" validator uni
    /// begona yozuv deb hisoblab TEACHER_BUSY berib yuboradi.
    /// </summary>
    [Fact]
    public async Task Ozini_ozi_kochirganda_TEACHER_BUSY_bermaydi()
    {
        // Arrange — mavjud yozuvning aynan o'zi draft sifatida beriladi (Id to'ldirilgan).
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group);
        var entry = db.AddEntry(group, subject, teacher, WeekDay.Dushanba, 1, "101");

        var draft = new ScheduleEntryDraft(entry.Id, group.Id, subject.Id, teacher.Id, WeekDay.Dushanba, 1, "101");

        // Act
        var result = await db.Get<IScheduleValidator>().ValidateAsync(draft);

        // Assert — o'z-o'zi bilan konflikt bo'lmasligi kerak.
        Assert.False(Has(result, ConflictCodes.TeacherBusy));
        Assert.False(Has(result, ConflictCodes.ClassBusy));
        Assert.False(Has(result, ConflictCodes.RoomBusy));
        Assert.True(result.IsValid);
    }

    // -----------------------------------------------------------------
    // 6. CLASS_BUSY
    // -----------------------------------------------------------------
    [Fact]
    public async Task Sinf_shu_vaqtda_band_bolsa_CLASS_BUSY_beradi()
    {
        // Arrange — bitta sinf, ikki xil o'qituvchi, ayni kun va soat.
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher1 = db.AddTeacher("Birinchi O'qituvchi");
        var teacher2 = db.AddTeacher("Ikkinchi O'qituvchi");
        var subject1 = db.AddSubject("Matematika", "MAT");
        var subject2 = db.AddSubject("Fizika", "FIZ");
        var group = db.AddClassGroup();
        db.AddAssignment(teacher1, subject1, group);
        db.AddAssignment(teacher2, subject2, group);
        db.AddEntry(group, subject1, teacher1, WeekDay.Dushanba, 1);

        var draft = new ScheduleEntryDraft(null, group.Id, subject2.Id, teacher2.Id, WeekDay.Dushanba, 1, null);

        // Act
        var result = await db.Get<IScheduleValidator>().ValidateAsync(draft);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(Has(result, ConflictCodes.ClassBusy));
    }

    // -----------------------------------------------------------------
    // 7. ROOM_BUSY
    // -----------------------------------------------------------------
    [Fact]
    public async Task Xona_band_bolsa_ROOM_BUSY_beradi()
    {
        // Arrange — ayni kun/soatda "203" xonasi allaqachon egallangan.
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher1 = db.AddTeacher("Birinchi O'qituvchi");
        var teacher2 = db.AddTeacher("Ikkinchi O'qituvchi");
        var subject1 = db.AddSubject("Matematika", "MAT");
        var subject2 = db.AddSubject("Fizika", "FIZ");
        var groupA = db.AddClassGroup("5-A");
        var groupB = db.AddClassGroup("5-B");
        db.AddAssignment(teacher1, subject1, groupA);
        db.AddAssignment(teacher2, subject2, groupB);
        db.AddEntry(groupA, subject1, teacher1, WeekDay.Dushanba, 1, "203");

        var draft = new ScheduleEntryDraft(null, groupB.Id, subject2.Id, teacher2.Id, WeekDay.Dushanba, 1, "203");

        // Act
        var result = await db.Get<IScheduleValidator>().ValidateAsync(draft);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(Has(result, ConflictCodes.RoomBusy));
    }

    // -----------------------------------------------------------------
    // 8. TEACHER_UNAVAILABLE
    // -----------------------------------------------------------------
    [Fact]
    public async Task Oqituvchi_ish_vaqtidan_tashqarida_bolsa_TEACHER_UNAVAILABLE_beradi()
    {
        // Arrange — 1-dars 08:30–09:15, o'qituvchi esa faqat 10:00–14:00 da ishlaydi.
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group);
        db.AddAvailability(teacher, WeekDay.Dushanba, new TimeSpan(10, 0, 0), new TimeSpan(14, 0, 0));

        var draft = new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Dushanba, 1, null);

        // Act
        var result = await db.Get<IScheduleValidator>().ValidateAsync(draft);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(Has(result, ConflictCodes.TeacherUnavailable));
    }

    [Fact]
    public async Task Oqituvchi_ish_vaqti_ichida_bolsa_TEACHER_UNAVAILABLE_bermaydi()
    {
        // Arrange — 1-dars 08:30–09:15, o'qituvchi 08:00–15:00 da ishlaydi.
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group);
        db.AddAvailability(teacher, WeekDay.Dushanba, new TimeSpan(8, 0, 0), new TimeSpan(15, 0, 0));

        var draft = new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Dushanba, 1, null);

        // Act
        var result = await db.Get<IScheduleValidator>().ValidateAsync(draft);

        // Assert
        Assert.False(Has(result, ConflictCodes.TeacherUnavailable));
        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Kontrakt 2.2 (8): shu kun uchun o'qituvchida umuman yozuv bo'lmasa — cheklov yo'q.
    /// </summary>
    [Fact]
    public async Task Shu_kun_uchun_vaqt_yozuvi_bolmasa_TEACHER_UNAVAILABLE_bermaydi()
    {
        // Arrange — vaqt yozuvi faqat Seshanba uchun kiritilgan, dars esa Dushanbaga qo'yiladi.
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group);
        db.AddAvailability(teacher, WeekDay.Seshanba, new TimeSpan(10, 0, 0), new TimeSpan(11, 0, 0));

        var draft = new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Dushanba, 1, null);

        // Act
        var result = await db.Get<IScheduleValidator>().ValidateAsync(draft);

        // Assert — Dushanba uchun cheklov yo'q, demak konflikt ham yo'q.
        Assert.False(Has(result, ConflictCodes.TeacherUnavailable));
        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Qora ro'yxat: faqat "band" (IsAvailable = false) oraliq yozilgan bo'lsa,
    /// u faqat o'sha oraliqni to'sadi — kunning qolgan soatlari ochiq qoladi.
    /// (1-dars 08:30–09:15 band oraliq bilan kesishadi.)
    /// </summary>
    [Fact]
    public async Task Faqat_band_oraliq_bolsa_kesishuvchi_soat_TEACHER_UNAVAILABLE_beradi()
    {
        // Arrange — Dushanba 09:00–11:00 band; boshqa yozuv yo'q.
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group);
        db.AddAvailability(teacher, WeekDay.Dushanba, new TimeSpan(9, 0, 0), new TimeSpan(11, 0, 0), isAvailable: false);

        var draft = new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Dushanba, 1, null);

        // Act
        var result = await db.Get<IScheduleValidator>().ValidateAsync(draft);

        // Assert — 08:30–09:15 va 09:00–11:00 kesishadi.
        Assert.True(Has(result, ConflictCodes.TeacherUnavailable));
    }

    /// <summary>
    /// Qora ro'yxat kunning qolgan qismini to'smaydi: 7-dars (14:00–14:45)
    /// "band" oraliq bilan kesishmaydi, oq ro'yxat esa umuman yo'q.
    /// </summary>
    [Fact]
    public async Task Faqat_band_oraliq_bolsa_qolgan_soatlar_ochiq_qoladi()
    {
        // Arrange — Dushanba 09:00–11:00 band; oq ro'yxat (IsAvailable = true) yo'q.
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group);
        db.AddAvailability(teacher, WeekDay.Dushanba, new TimeSpan(9, 0, 0), new TimeSpan(11, 0, 0), isAvailable: false);

        var draft = new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Dushanba, 7, null);

        // Act
        var result = await db.Get<IScheduleValidator>().ValidateAsync(draft);

        // Assert — 14:00–14:45 erkin.
        Assert.False(Has(result, ConflictCodes.TeacherUnavailable));
        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Qora ro'yxat oq ro'yxatdan ustun: ish vaqti ichida bo'lsa ham,
    /// "band" oraliq bilan kesishsa konflikt beradi.
    /// </summary>
    [Fact]
    public async Task Band_oraliq_oq_royxatdan_ustun_boladi()
    {
        // Arrange — 08:00–15:00 ishlaydi, lekin 12:00–13:00 band. 5-dars: 12:10–12:55.
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group);
        db.AddAvailability(teacher, WeekDay.Dushanba, new TimeSpan(8, 0, 0), new TimeSpan(15, 0, 0));
        db.AddAvailability(teacher, WeekDay.Dushanba, new TimeSpan(12, 0, 0), new TimeSpan(13, 0, 0), isAvailable: false);

        var draft = new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Dushanba, 5, null);

        // Act
        var result = await db.Get<IScheduleValidator>().ValidateAsync(draft);

        // Assert
        Assert.True(Has(result, ConflictCodes.TeacherUnavailable));
    }

    [Fact]
    public async Task Band_oraliqdan_tashqaridagi_soat_ish_vaqti_ichida_bolsa_otadi()
    {
        // Arrange — 08:00–15:00 ishlaydi, 12:00–13:00 band. 1-dars: 08:30–09:15.
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group);
        db.AddAvailability(teacher, WeekDay.Dushanba, new TimeSpan(8, 0, 0), new TimeSpan(15, 0, 0));
        db.AddAvailability(teacher, WeekDay.Dushanba, new TimeSpan(12, 0, 0), new TimeSpan(13, 0, 0), isAvailable: false);

        var draft = new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Dushanba, 1, null);

        // Act
        var result = await db.Get<IScheduleValidator>().ValidateAsync(draft);

        // Assert
        Assert.False(Has(result, ConflictCodes.TeacherUnavailable));
        Assert.True(result.IsValid);
    }

    // -----------------------------------------------------------------
    // 9. WEEKLY_HOURS_EXCEEDED (Warning)
    // -----------------------------------------------------------------
    [Fact]
    public async Task Haftalik_soat_meyoridan_oshsa_WEEKLY_HOURS_EXCEEDED_ogohlantirishini_beradi()
    {
        // Arrange — biriktirmada haftasiga 1 soat, 1 ta dars allaqachon qo'yilgan.
        // Ikkinchisi boshqa kunga qo'yiladi (fan takrorlanishi bilan aralashmasligi uchun).
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group, weeklyHours: 1);
        db.AddEntry(group, subject, teacher, WeekDay.Dushanba, 1);

        var draft = new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Seshanba, 1, null);

        // Act
        var result = await db.Get<IScheduleValidator>().ValidateAsync(draft);

        // Assert — bu faqat ogohlantirish, Error emas.
        Assert.True(Has(result, ConflictCodes.WeeklyHoursExceeded));
        Assert.True(result.HasWarnings);
        Assert.True(result.IsValid);
    }

    // -----------------------------------------------------------------
    // 10. SUBJECT_REPEATED_IN_DAY (Warning)
    // -----------------------------------------------------------------
    [Fact]
    public async Task Fan_shu_kuni_takrorlansa_SUBJECT_REPEATED_IN_DAY_ogohlantirishini_beradi()
    {
        // Arrange — shu sinfda shu fan Dushanba 1-darsda bor, yana 3-darsga qo'yilmoqda.
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group, weeklyHours: 10);
        db.AddEntry(group, subject, teacher, WeekDay.Dushanba, 1);

        var draft = new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Dushanba, 3, null);

        // Act
        var result = await db.Get<IScheduleValidator>().ValidateAsync(draft);

        // Assert
        Assert.True(Has(result, ConflictCodes.SubjectRepeatedInDay));
        Assert.True(result.HasWarnings);
        Assert.True(result.IsValid);
    }

    // -----------------------------------------------------------------
    // Ijobiy stsenariy
    // -----------------------------------------------------------------
    [Fact]
    public async Task Togri_joylashtirishda_hech_qanday_konflikt_bolmaydi()
    {
        // Arrange
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group, weeklyHours: 5);

        var draft = new ScheduleEntryDraft(null, group.Id, subject.Id, teacher.Id, WeekDay.Dushanba, 2, "101");

        // Act
        var result = await db.Get<IScheduleValidator>().ValidateAsync(draft);

        // Assert
        Assert.True(result.IsValid);
        Assert.False(result.HasWarnings);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public async Task Bosh_jadvalda_ValidateAllAsync_konfliktsiz_qaytadi()
    {
        // Arrange — bitta ham yozuv yo'q.
        using var db = new TestDbFactory();
        db.SeedDefaults();

        // Act
        var result = await db.Get<IScheduleValidator>().ValidateAllAsync();

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAllAsync_mavjud_konfliktni_topadi()
    {
        // Arrange — bazaga to'g'ridan-to'g'ri (validatordan chetlab) nofaol kunga yozuv qo'yamiz.
        using var db = new TestDbFactory();
        db.SeedDefaults();
        var teacher = db.AddTeacher();
        var subject = db.AddSubject();
        var group = db.AddClassGroup();
        db.AddAssignment(teacher, subject, group);
        db.AddEntry(group, subject, teacher, WeekDay.Yakshanba, 1);

        // Act
        var result = await db.Get<IScheduleValidator>().ValidateAllAsync();

        // Assert
        Assert.False(result.IsValid);
        Assert.True(Has(result, ConflictCodes.DayInactive));
    }
}
