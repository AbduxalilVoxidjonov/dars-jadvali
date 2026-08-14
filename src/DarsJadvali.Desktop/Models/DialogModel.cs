namespace DarsJadvali.Desktop.Models;

/// <summary>Muloqot oynasidagi bitta qator (masalan, bitta konflikt).</summary>
public sealed class DialogLine
{
    public string Text { get; init; } = string.Empty;

    /// <summary>Qator rangi — "#RRGGBB".</summary>
    public string ColorCode { get; init; } = "#212121";
}

/// <summary><see cref="Views.DialogWindow"/> uchun ma'lumot modeli.</summary>
public sealed class DialogModel
{
    public string Title { get; init; } = "Ma'lumot";

    /// <summary>Asosiy matn (ixtiyoriy).</summary>
    public string? Message { get; init; }

    /// <summary>Asosiy matn rangi.</summary>
    public string MessageColorCode { get; init; } = "#212121";

    /// <summary>Alohida qatorlar (validatsiya natijalari uchun).</summary>
    public IReadOnlyList<DialogLine> Lines { get; init; } = Array.Empty<DialogLine>();

    /// <summary>Asosiy (tasdiqlovchi) tugma matni.</summary>
    public string PrimaryText { get; init; } = "Yopish";

    /// <summary>Ikkinchi (bekor qiluvchi) tugma matni. Null bo'lsa tugma ko'rinmaydi.</summary>
    public string? SecondaryText { get; init; }

    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);

    public bool HasLines => Lines.Count > 0;

    public bool HasSecondary => !string.IsNullOrWhiteSpace(SecondaryText);
}
