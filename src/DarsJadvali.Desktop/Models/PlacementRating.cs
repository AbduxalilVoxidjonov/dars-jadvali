namespace DarsJadvali.Desktop.Models;

/// <summary>
/// Pozitsiyani baholash — aSc'dagi <b>kulrang / ko'k / yashil</b> mantiqining aynan o'zi
/// (<c>docs/research/03-asc-features-ux.md</c> §4.1).
/// </summary>
/// <remarks>
/// ViewModel rang qaytarmaydi — faqat shu enum. Rangni
/// <c>Converters/PlacementRatingToBrushConverter</c> hal qiladi (M-06).
/// </remarks>
public enum PlacementRating
{
    /// <summary>Kulrang — taqiqlangan pozitsiya (sinf/o'qituvchi band, kun yopiq va h.k.).</summary>
    Forbidden = 0,

    /// <summary>Ko'k — ruxsat etilgan, lekin ogohlantirish bor ("yaxshi emas").</summary>
    Allowed = 1,

    /// <summary>Yashil — bu karta uchun yaxshi pozitsiya.</summary>
    Preferred = 2,
}

/// <summary>Bitta pozitsiyani baholash natijasi: daraja + sabablar.</summary>
/// <param name="Rating">Baho darajasi.</param>
/// <param name="Reasons">Foydalanuvchiga ko'rsatiladigan sabablar (o'zbekcha).</param>
public sealed record PlacementEvaluation(PlacementRating Rating, IReadOnlyList<string> Reasons)
{
    /// <summary>Sababsiz "yashil" natija.</summary>
    public static PlacementEvaluation Preferred { get; } =
        new(PlacementRating.Preferred, Array.Empty<string>());

    /// <summary>Qo'yish mumkinmi (kulrang bo'lmasa — mumkin).</summary>
    public bool IsAllowed => Rating != PlacementRating.Forbidden;

    /// <summary>Sabablarni bitta matnga yig'adi.</summary>
    public string ReasonText => Reasons.Count == 0 ? string.Empty : string.Join(Environment.NewLine, Reasons);

    /// <summary>Taqiqlangan natija yasaydi.</summary>
    public static PlacementEvaluation Forbid(params string[] reasons) =>
        new(PlacementRating.Forbidden, reasons);

    /// <summary>Ogohlantirishli ("ko'k") natija yasaydi.</summary>
    public static PlacementEvaluation Warn(IReadOnlyList<string> reasons) =>
        new(PlacementRating.Allowed, reasons);
}

/// <summary>Jadval ko'rinishi — qaysi resurs bo'yicha qatorlar quriladi.</summary>
public enum TimetableViewKind
{
    /// <summary>Sinflar bo'yicha (har qator — bitta sinf).</summary>
    Class,

    /// <summary>O'qituvchilar bo'yicha.</summary>
    Teacher,

    /// <summary>Xonalar bo'yicha.</summary>
    Room,

    /// <summary>Umumiy jadval — barcha sinflar ketma-ket.</summary>
    All,
}
