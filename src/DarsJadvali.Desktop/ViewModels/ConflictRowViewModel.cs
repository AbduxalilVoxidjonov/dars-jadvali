using DarsJadvali.Application.Validation;

namespace DarsJadvali.Desktop.ViewModels;

/// <summary>Tekshiruv natijasidagi bitta konflikt.</summary>
/// <remarks>
/// Rang qaytarilmaydi — faqat <see cref="Severity"/>. Ranglarni
/// <c>ConflictSeverityToBrushConverter</c> hal qiladi (M-06).
/// </remarks>
public sealed class ConflictRowViewModel
{
    /// <summary>Konfliktdan qator yasaydi.</summary>
    public ConflictRowViewModel(Conflict conflict)
    {
        ArgumentNullException.ThrowIfNull(conflict);

        Code = conflict.Code;
        Message = conflict.Message;
        Severity = conflict.Severity;
    }

    /// <summary>Konflikt kodi ("TEACHER_BUSY").</summary>
    public string Code { get; }

    /// <summary>Foydalanuvchiga ko'rinadigan izoh.</summary>
    public string Message { get; }

    /// <summary>Konflikt darajasi — ranglar shu asosda tanlanadi.</summary>
    public ConflictSeverity Severity { get; }

    /// <summary>Xatomi (aks holda — ogohlantirish).</summary>
    public bool IsError => Severity == ConflictSeverity.Error;

    /// <summary>"Xato" yoki "Ogohlantirish".</summary>
    public string SeverityText => IsError ? "Xato" : "Ogohlantirish";
}
