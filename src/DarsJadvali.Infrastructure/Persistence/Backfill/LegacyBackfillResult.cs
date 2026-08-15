namespace DarsJadvali.Infrastructure.Persistence.Backfill;

/// <summary>Ma'lumot ko'chirish natijasi — tekshiruv va hisobot uchun.</summary>
public sealed class LegacyBackfillResult
{
    /// <summary>Yaratilgan choraklar soni.</summary>
    public int Terms { get; set; }

    /// <summary>Yaratilgan smenalar soni.</summary>
    public int Shifts { get; set; }

    /// <summary>Yaratilgan dars soatlari soni.</summary>
    public int Periods { get; set; }

    /// <summary>Yaratilgan sinflar soni.</summary>
    public int SchoolClasses { get; set; }

    /// <summary>Yaratilgan bo'linishlar soni.</summary>
    public int ClassDivisions { get; set; }

    /// <summary>Yaratilgan guruhlar soni.</summary>
    public int StudentGroups { get; set; }

    /// <summary>Yaratilgan dars ta'riflari soni.</summary>
    public int Lessons { get; set; }

    /// <summary>Biriktirmasiz (yetim) yozuvlar uchun avtomatik yaratilgan darslar soni.</summary>
    public int OrphanLessons { get; set; }

    /// <summary>Yaratilgan kartochkalar soni.</summary>
    public int Cards { get; set; }

    /// <summary>Yaratilgan bandlik qatorlari soni.</summary>
    public int CardOccurrences { get; set; }

    /// <summary>
    /// <c>V2_06</c>: eski <c>TeacherAvailability</c> oraliqlaridan hosil qilingan
    /// <c>TimeOff</c> katakchalari soni.
    /// </summary>
    public int TimeOffs { get; set; }

    /// <summary><c>V2_07</c>: matn xona nomlaridan yaratilgan <c>Classroom</c> yozuvlari soni.</summary>
    public int Classrooms { get; set; }

    /// <summary><c>V2_07</c>: kartochkaga tayinlangan xona bog'lanishlari soni.</summary>
    public int CardClassrooms { get; set; }

    /// <summary>
    /// <c>V2_07</c>: xona bandligi to'qnashgani uchun TAYINLANMAGAN kartochkalar soni.
    /// Eski modelda xona bandligi umuman tekshirilmagan, shuning uchun haqiqiy bazada
    /// bir xonada ikki dars uchrashi mumkin. Bunday kartochka yo'qotilmaydi —
    /// faqat xonasiz qoladi va bu yerda sanaladi.
    /// </summary>
    public int RoomConflicts { get; set; }

    /// <summary>O'zbekcha diagnostika xabarlari.</summary>
    public List<string> Messages { get; } = new();
}
