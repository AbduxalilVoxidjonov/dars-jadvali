using CommunityToolkit.Mvvm.ComponentModel;

namespace DarsJadvali.Desktop.ViewModels;

/// <summary>Katak zichligi — aSc'dagi "compact / normal" ko'rinishlariga mos.</summary>
public enum TimetableDensity
{
    /// <summary>Zich — ko'proq qator sig'adi.</summary>
    Zich,

    /// <summary>Odatiy.</summary>
    Oddiy,

    /// <summary>Keng — matn yaxshi o'qiladi.</summary>
    Keng,
}

/// <summary>
/// To'r o'lchamlarining <b>yagona manbasi</b> — WPF'dagi <c>SharedSizeGroup</c> ning ekvivalenti.
/// </summary>
/// <remarks>
/// <para>
/// M-04: eski kod ustun kengliklarini qat'iy pikselda (136/104/150) code-behind'da bergan,
/// chunki "Avalonia'da <c>SharedSizeGroup</c> yo'q". Yechim — o'lchamni <b>bitta obyektda</b>
/// saqlash va sarlavha qatori bilan tana kataklarini <b>o'sha bitta obyektga bog'lash</b>:
/// shunda ustunlar avtomatik moslashadi va qat'iy piksel kerak emas.
/// </para>
/// <para>
/// Zoom (<c>+</c> / <c>-</c>) va zichlik sozlamasi shu yerda — bitta xossani o'zgartirish
/// butun to'rni qayta o'lchaydi, hech narsa qayta qurilmaydi.
/// </para>
/// </remarks>
public sealed partial class TimetableMetrics : ObservableObject
{
    /// <summary>Eng kichik masshtab.</summary>
    public const double MinZoom = 0.5;

    /// <summary>Eng katta masshtab.</summary>
    public const double MaxZoom = 2.0;

    /// <summary>Masshtab qadami (<c>+</c> / <c>-</c>).</summary>
    public const double ZoomStep = 0.1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CellWidth))]
    [NotifyPropertyChangedFor(nameof(RowHeight))]
    [NotifyPropertyChangedFor(nameof(HeaderWidth))]
    [NotifyPropertyChangedFor(nameof(PeriodWidth))]
    [NotifyPropertyChangedFor(nameof(TitleFontSize))]
    [NotifyPropertyChangedFor(nameof(DetailFontSize))]
    [NotifyPropertyChangedFor(nameof(ZoomText))]
    private double _zoom = 1.0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RowHeight))]
    private TimetableDensity _density = TimetableDensity.Oddiy;

    /// <summary>Matn ranglari invert qilinganmi (aSc'dagi <c>*</c> tugmasi).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NormalLayerOpacity))]
    [NotifyPropertyChangedFor(nameof(InvertedLayerOpacity))]
    private bool _isInverted;

    /// <summary>Oddiy (ochiq fonli) qatlam ko'rinadimi.</summary>
    /// <remarks>
    /// <c>IsVisible</c> o'rniga shaffoflik ishlatiladi: shunda ikkala qatlam ham o'lchovda
    /// qoladi va invert qilinganda to'r "sakramaydi".
    /// </remarks>
    public double NormalLayerOpacity => IsInverted ? 0 : 1;

    /// <summary>Invert qilingan (to'q fonli) qatlam ko'rinadimi.</summary>
    public double InvertedLayerOpacity => IsInverted ? 1 : 0;

    /// <summary>Kun ustunining kengligi.</summary>
    public double CellWidth => Math.Round(148 * Zoom);

    /// <summary>Sinf/o'qituvchi nomi ustunining kengligi.</summary>
    public double HeaderWidth => Math.Round(150 * Zoom);

    /// <summary>Dars raqami ustunining kengligi.</summary>
    public double PeriodWidth => Math.Round(84 * Zoom);

    /// <summary>Qator balandligi — zichlik va masshtabga bog'liq.</summary>
    public double RowHeight => Math.Round(BaseRowHeight * Zoom);

    /// <summary>Kartadagi fan nomi shrifti.</summary>
    public double TitleFontSize => Math.Round(12 * Zoom, 1);

    /// <summary>Kartadagi qo'shimcha matn shrifti.</summary>
    public double DetailFontSize => Math.Round(10 * Zoom, 1);

    /// <summary>Masshtabning foizdagi ko'rinishi ("100%").</summary>
    public string ZoomText => $"{Zoom * 100:0}%";

    /// <summary>Butun to'rning kengligi (sarlavha ustunlari + kun ustunlari).</summary>
    public double TotalWidth(int dayCount) => HeaderWidth + PeriodWidth + (CellWidth * dayCount);

    /// <summary>Masshtabni bir qadam kattalashtiradi.</summary>
    public void ZoomIn() => Zoom = Math.Min(MaxZoom, Math.Round(Zoom + ZoomStep, 2));

    /// <summary>Masshtabni bir qadam kichiklashtiradi.</summary>
    public void ZoomOut() => Zoom = Math.Max(MinZoom, Math.Round(Zoom - ZoomStep, 2));

    /// <summary>Masshtabni asl holiga qaytaradi.</summary>
    public void ZoomReset() => Zoom = 1.0;

    private double BaseRowHeight => Density switch
    {
        TimetableDensity.Zich => 34,
        TimetableDensity.Keng => 66,
        _ => 48,
    };
}
