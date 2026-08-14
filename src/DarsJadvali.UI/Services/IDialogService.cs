using DarsJadvali.Application.Validation;

namespace DarsJadvali.UI.Services;

/// <summary>Foydalanuvchi bilan muloqot oynalari.</summary>
public interface IDialogService
{
    /// <summary>Ha/Yo'q so'roq oynasi. "Ha" bosilsa true qaytadi.</summary>
    bool Confirm(string message, string title = "Tasdiqlang");

    /// <summary>Oddiy ma'lumot oynasi.</summary>
    void Info(string message, string title = "Ma'lumot");

    /// <summary>Xatolik oynasi.</summary>
    void Error(string message, string title = "Xatolik");

    /// <summary>Validatsiya natijasini konflikt darajasiga qarab ko'rsatadi.</summary>
    void ShowValidation(ValidationResult result, string title = "Tekshiruv natijasi");
}
