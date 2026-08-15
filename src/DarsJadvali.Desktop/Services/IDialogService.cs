using DarsJadvali.Application.Validation;

namespace DarsJadvali.Desktop.Services;

/// <summary>Foydalanuvchi bilan muloqot oynalari (Avalonia'da MessageBox yo'q).</summary>
public interface IDialogService
{
    /// <summary>Oddiy ma'lumot oynasi.</summary>
    Task InfoAsync(string message, string title = "Ma'lumot");

    /// <summary>Xato oynasi.</summary>
    Task ErrorAsync(string message, string title = "Xato");

    /// <summary>Ha/Yo'q so'roq oynasi. "Ha" bosilsa true qaytadi.</summary>
    Task<bool> ConfirmAsync(string message, string title = "Tasdiqlang");

    /// <summary>Validatsiya natijasini ko'rsatadi: Error qizil, Warning sariq.</summary>
    Task ShowValidationAsync(ValidationResult result);

    /// <summary>Ogohlantirishlarni ko'rsatib "Baribir qo'yilsinmi?" deb so'raydi.</summary>
    Task<bool> ConfirmWarningsAsync(ValidationResult result);

    /// <summary>Matnni almashish buferiga (clipboard) nusxalaydi.</summary>
    Task CopyToClipboardAsync(string text);

    /// <summary>PDF saqlash uchun fayl tanlash dialogi. Bekor qilinsa null.</summary>
    Task<string?> SaveFileAsync(string suggestedFileName, string filterName = "PDF hujjat", string extension = "pdf");

    /// <summary>
    /// Mavjud faylni ochish dialogi (Avalonia <c>IStorageProvider</c>).
    /// Tanlangan faylning to'liq yo'lini qaytaradi; bekor qilinsa <c>null</c>.
    /// </summary>
    /// <param name="title">Oyna sarlavhasi.</param>
    /// <param name="filterName">Filtr nomi (masalan "aSc TimeTables XML").</param>
    /// <param name="extension">Kengaytma, nuqtasiz (masalan "xml").</param>
    Task<string?> OpenFileAsync(
        string title = "Faylni tanlang",
        string filterName = "XML fayl",
        string extension = "xml");
}
