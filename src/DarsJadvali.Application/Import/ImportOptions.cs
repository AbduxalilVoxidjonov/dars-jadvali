namespace DarsJadvali.Application.Import;

/// <summary>
/// Mavjud ma'lumot bilan nima qilish.
/// </summary>
public enum ImportMergeMode
{
    /// <summary>
    /// <b>Birlashtirish (standart).</b> Hech narsa o'chirilmaydi: mavjud yozuvlar
    /// <c>ExternalId</c> bo'yicha topilib yangilanadi, yo'qlari yaratiladi.
    /// </summary>
    Merge = 0,

    /// <summary>
    /// <b>Almashtirish.</b> Import boshlanishidan oldin maqsad o'quv yilining BARCHA
    /// <c>Lesson</c> yozuvlari (va ular bilan birga kaskad bo'yicha barcha
    /// <c>Card</c> / <c>CardOccurrence</c> qatorlari) o'chiriladi, so'ng XML'dagilar
    /// qaytadan yoziladi.
    /// </summary>
    /// <remarks>
    /// <b>Diqqat — buzg'unchi rejim.</b> Ma'lumotnomalar (fan, o'qituvchi, sinf, guruh,
    /// xona, parallel, dars soati, chorak) hech qachon o'chirilmaydi — ular ikkala
    /// rejimda ham faqat yangilanadi yoki yaratiladi. Faqat "reja + jadval" qatlami
    /// tozalanadi, chunki aynan o'shanda yetim kartochkalar qolib ketadi.
    /// </remarks>
    Replace = 1
}

/// <summary>
/// aSc XML importi parametrlari.
/// </summary>
/// <remarks>
/// Kontrakt Desktop agenti uchun BARQAROR: yangi maydonlar faqat standart qiymat bilan
/// qo'shiladi, mavjudlari o'chirilmaydi va ma'nosi o'zgarmaydi.
/// </remarks>
public sealed record ImportOptions
{
    /// <summary>
    /// <b>Majburiy.</b> Maqsad o'quv yili Id — barcha yozuvlar shu yilga bog'lanadi.
    /// </summary>
    public required int AcademicYearId { get; init; }

    /// <summary>Mavjud ma'lumot bilan nima qilish. Standart — <see cref="ImportMergeMode.Merge"/>.</summary>
    public ImportMergeMode MergeMode { get; init; } = ImportMergeMode.Merge;

    /// <summary>
    /// O'quvchilarni (<c>students</c>, <c>studentsubjects</c>) o'tkazib yuborish.
    /// Hozircha <b>har doim</b> o'tkazib yuboriladi (P2) — <c>false</c> qilinsa ham
    /// faqat ogohlantirish yoziladi.
    /// </summary>
    public bool SkipStudents { get; init; } = true;

    /// <summary>
    /// Kartochkalarni (<c>cards</c>) import qilish. <c>false</c> — faqat reja
    /// (<c>lessons</c>) yuklanadi, jadval bo'sh qoladi va uni generator to'ldiradi.
    /// </summary>
    public bool ImportCards { get; init; } = true;

    /// <summary>
    /// <b>Oldindan ko'rish rejimi.</b> Butun import tranzaksiya ichida bajariladi va
    /// oxirida QAYTARILADI — bazaga hech narsa yozilmaydi, lekin hisobot haqiqiy.
    /// </summary>
    public bool DryRun { get; init; }

    /// <summary>
    /// Yaratiladigan jadval variantlari nomining boshlanishi. Har chorak uchun
    /// <c>"{prefiks} — {chorak nomi}"</c> ko'rinishida variant hosil bo'ladi.
    /// </summary>
    public string SchedulePrefix { get; init; } = "aSc import";

    /// <summary>
    /// Import tugagach birinchi (eng kichik chorak raqamli) jadval variantini faol qilish.
    /// Standart <c>false</c> — mavjud faol jadval o'zgarmaydi.
    /// </summary>
    public bool ActivateFirstSchedule { get; init; }

    /// <summary>Faqat oldindan ko'rish uchun parametrlar.</summary>
    public static ImportOptions ForPreview(int academicYearId) =>
        new() { AcademicYearId = academicYearId, DryRun = true };
}
