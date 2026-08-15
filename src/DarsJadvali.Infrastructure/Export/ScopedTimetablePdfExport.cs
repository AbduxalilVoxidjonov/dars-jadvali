using DarsJadvali.Application.Export;

namespace DarsJadvali.Infrastructure.Export;

/// <summary>Tayyor PDF hujjat: mazmuni va tavsiya etilgan fayl nomi.</summary>
/// <param name="Content">PDF baytlari.</param>
/// <param name="FileName">Saqlash uchun tavsiya etilgan nom (qamrov nomi bilan).</param>
public sealed record TimetablePdfDocument(byte[] Content, string FileName);

/// <summary>
/// Qamrovi ANIQ ko'rsatilgan PDF eksport.
/// <para>
/// Sabab: eski <see cref="ISchoolTimetablePdfExporter.ExportAsync"/> da qamrov
/// <c>PdfExportOptions.ClassGroupId</c> orqali beriladi va u <c>null</c> bo'lsa
/// JIMGINA butun maktab jadvali chiqadi. Chaqiruvchi sinf tanlashni unutsa yoki
/// tanlangan sinf Id'si yo'qolsa — foydalanuvchi kutgan bitta sinf o'rniga butun
/// maktab ma'lumoti chop etiladi. Bu yerdagi uchta metodda qamrov TASODIFAN
/// kengayib ketmaydi: har biri o'z qamrovini nomida aytadi va noto'g'ri qiymatda
/// <see cref="ArgumentException"/> tashlaydi.
/// </para>
/// </summary>
public interface IScopedTimetablePdfExporter
{
    /// <summary>Bitta sinfning jadvali.</summary>
    /// <param name="classGroupId">Sinf Id — musbat bo'lishi shart.</param>
    /// <param name="options">Qo'shimcha sozlamalar (qamrov e'tiborga olinmaydi).</param>
    /// <param name="ct">Bekor qilish belgisi.</param>
    /// <exception cref="ArgumentException">Sinf Id ko'rsatilmagan (0 yoki manfiy).</exception>
    Task<TimetablePdfDocument> ExportClassScheduleAsync(
        int classGroupId, PdfExportOptions? options = null, CancellationToken ct = default);

    /// <summary>Bitta o'qituvchining jadvali.</summary>
    /// <param name="teacherId">O'qituvchi Id — musbat bo'lishi shart.</param>
    /// <param name="options">Qo'shimcha sozlamalar (qamrov e'tiborga olinmaydi).</param>
    /// <param name="ct">Bekor qilish belgisi.</param>
    /// <exception cref="ArgumentException">O'qituvchi Id ko'rsatilmagan yoki topilmadi.</exception>
    Task<TimetablePdfDocument> ExportTeacherScheduleAsync(
        int teacherId, PdfExportOptions? options = null, CancellationToken ct = default);

    /// <summary>Butun maktab jadvali — qamrov ATAYLAB keng ekani metod nomida ko'rinadi.</summary>
    /// <param name="options">Qo'shimcha sozlamalar (qamrov e'tiborga olinmaydi).</param>
    /// <param name="ct">Bekor qilish belgisi.</param>
    Task<TimetablePdfDocument> ExportSchoolScheduleAsync(
        PdfExportOptions? options = null, CancellationToken ct = default);
}
