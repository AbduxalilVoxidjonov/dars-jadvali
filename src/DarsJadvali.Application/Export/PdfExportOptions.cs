namespace DarsJadvali.Application.Export;

/// <summary>PDF eksport sozlamalari.</summary>
public sealed record PdfExportOptions
{
    /// <summary>Faqat bitta sinf uchun eksport. <c>null</c> — barcha sinflar.</summary>
    public int? ClassGroupId { get; init; }

    /// <summary>Sarlavhada chiqadigan maktab nomi.</summary>
    public string? SchoolName { get; init; }

    /// <summary>Landshaft (ko'ndalang) yo'nalish. Standart — ha.</summary>
    public bool Landscape { get; init; } = true;

    /// <summary>Katakda o'qituvchi ismi ko'rsatilsinmi.</summary>
    public bool IncludeTeacherName { get; init; } = true;

    /// <summary>Katakda xona raqami ko'rsatilsinmi.</summary>
    public bool IncludeRoom { get; init; } = true;

    /// <summary>Qaysi dars jadvali (varianti) chizilsin. <c>null</c> — faol jadval.</summary>
    public int? ScheduleId { get; init; }
}

/// <summary>Maktab dars jadvalini PDF ga eksport qiluvchi.</summary>
public interface ISchoolTimetablePdfExporter
{
    /// <summary>Jadvalni PDF ga aylantirib, baytlar ko'rinishida qaytaradi.</summary>
    Task<byte[]> ExportAsync(PdfExportOptions options, CancellationToken ct = default);

    /// <summary>Tavsiya etilgan fayl nomi, masalan "Maktab-jadvali-2026-08-13.pdf".</summary>
    string SuggestFileName(PdfExportOptions options, DateTime now);
}
