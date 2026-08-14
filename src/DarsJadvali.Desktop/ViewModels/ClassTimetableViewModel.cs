using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using DarsJadvali.Application.Validation;

namespace DarsJadvali.Desktop.ViewModels;

/// <summary>Bosh sahifadagi sinf filtri bandi ("Barcha sinflar" uchun Id = 0).</summary>
/// <param name="Id">Sinf identifikatori; 0 bo'lsa — barcha sinflar.</param>
/// <param name="Name">Ro'yxatda ko'rinadigan nom.</param>
public sealed record ClassFilterOption(int Id, string Name);

/// <summary>
/// Bosh sahifadagi umumiy maktab jadvalining bir martalik "surati".
/// View (code-behind) shu obyekt asosida bitta <c>Grid</c> quradi.
/// </summary>
public sealed class SchoolTimetableSnapshot
{
    /// <summary>Faol ish kunlari sarlavhalari ("Dushanba", "Seshanba", ...).</summary>
    public IReadOnlyList<string> DayHeaders { get; init; } = Array.Empty<string>();

    /// <summary>Ekranda ko'rsatiladigan sinf bloklari (filtrdan o'tgan).</summary>
    public IReadOnlyList<ClassTimetableViewModel> Blocks { get; init; } = Array.Empty<ClassTimetableViewModel>();

    /// <summary>Chizadigan narsa yo'q.</summary>
    public bool IsEmpty => Blocks.Count == 0 || DayHeaders.Count == 0;
}

/// <summary>Umumiy maktab jadvalidagi bitta sinf guruhi (bir nechta soat qatoridan iborat).</summary>
public sealed class ClassTimetableViewModel
{
    /// <summary>Sinf identifikatori.</summary>
    public int ClassGroupId { get; init; }

    /// <summary>Sinf nomi ("5-A").</summary>
    public string ClassName { get; init; } = string.Empty;

    /// <summary>Sinfning asosiy xonasi ("101-xona"), bo'sh bo'lishi mumkin.</summary>
    public string RoomText { get; init; } = string.Empty;

    /// <summary>Xona matni bormi.</summary>
    public bool HasRoomText => !string.IsNullOrWhiteSpace(RoomText);

    /// <summary>Shu sinfga qo'yilgan darslar soni.</summary>
    public int LessonCount { get; init; }

    /// <summary>Guruhlar navbatma-navbat ozgina boshqa fonda ko'rsatiladi.</summary>
    public bool IsAlternate { get; set; }

    /// <summary>Sinf nomi ostidagi qisqa izoh.</summary>
    public string SummaryText => LessonCount > 0
        ? LessonCount + " ta dars"
        : "Dars yo'q";

    /// <summary>Sinfning soat qatorlari (1 dan maksimal dars raqamigacha).</summary>
    public ObservableCollection<ClassTimetableRowViewModel> Rows { get; } = new();
}

/// <summary>Sinf jadvalidagi bitta soat qatori.</summary>
public sealed class ClassTimetableRowViewModel
{
    /// <summary>Dars raqami matni ("3-soat").</summary>
    public string LessonText { get; init; } = string.Empty;

    /// <summary>Dars vaqti ("10:20-11:05"), topilmasa bo'sh.</summary>
    public string TimeText { get; init; } = string.Empty;

    /// <summary>Vaqt matni bormi.</summary>
    public bool HasTimeText => !string.IsNullOrWhiteSpace(TimeText);

    /// <summary>Har bir faol kun uchun bitta katak.</summary>
    public ObservableCollection<DashboardCellViewModel> Cells { get; } = new();
}

/// <summary>Maktab jadvalidagi bitta katak — faqat ko'rish, tahrirlash yo'q.</summary>
public sealed class DashboardCellViewModel
{
    /// <summary>Fan nomi.</summary>
    public string SubjectName { get; init; } = string.Empty;

    /// <summary>O'qituvchi FIO qisqartmasi.</summary>
    public string TeacherName { get; init; } = string.Empty;

    /// <summary>Xona raqami (bo'sh bo'lishi mumkin).</summary>
    public string RoomText { get; init; } = string.Empty;

    /// <summary>Xonani ko'rsatadigan matn ("Xona: 12").</summary>
    public string RoomDisplayText => HasRoom ? "Xona: " + RoomText : string.Empty;

    /// <summary>Xona ko'rsatilsinmi.</summary>
    public bool HasRoom => !string.IsNullOrWhiteSpace(RoomText);

    /// <summary>Katak foni uchun o'qituvchi rang kodi.</summary>
    public string ColorCode { get; init; } = "#FFFFFF";

    /// <summary>Katakda dars bormi.</summary>
    public bool HasEntry { get; init; }

    /// <summary>Katak foni — o'qituvchi rangining ochiq toni.</summary>
    public IBrush Background => HasEntry ? ScheduleColors.Light(ColorCode) : Brushes.Transparent;
}

/// <summary>Tekshiruv natijasidagi bitta konflikt (rangi bilan).</summary>
public sealed class ConflictRowViewModel
{
    /// <summary>Konfliktdan qator yasaydi.</summary>
    public ConflictRowViewModel(Conflict conflict)
    {
        ArgumentNullException.ThrowIfNull(conflict);

        Code = conflict.Code;
        Message = conflict.Message;
        IsError = conflict.Severity == ConflictSeverity.Error;
    }

    /// <summary>Konflikt kodi ("TEACHER_BUSY").</summary>
    public string Code { get; }

    /// <summary>Foydalanuvchiga ko'rinadigan izoh.</summary>
    public string Message { get; }

    /// <summary>Xatomi (aks holda — ogohlantirish).</summary>
    public bool IsError { get; }

    /// <summary>"Xato" yoki "Ogohlantirish".</summary>
    public string SeverityText => IsError ? "Xato" : "Ogohlantirish";

    /// <summary>Chap chiziq va matn rangi.</summary>
    public IBrush AccentBrush => IsError ? ScheduleColors.Error : ScheduleColors.Warning;

    /// <summary>Fon rangi.</summary>
    public IBrush BackgroundBrush => IsError ? ScheduleColors.ErrorBackground : ScheduleColors.WarningBackground;
}

/// <summary>Jadval kataklari uchun ranglar (konverterlarsiz — to'g'ridan-to'g'ri ViewModel'dan).</summary>
public static class ScheduleColors
{
    private static readonly Dictionary<string, IBrush> Cache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly IBrush Fallback = Frozen(Color.FromRgb(0xEC, 0xEF, 0xF1));

    /// <summary>Xato rangi (qizil).</summary>
    public static IBrush Error { get; } = Frozen(Color.Parse("#C62828"));

    /// <summary>Ogohlantirish rangi (sariq-jigarrang).</summary>
    public static IBrush Warning { get; } = Frozen(Color.Parse("#EF6C00"));

    /// <summary>Xato foni.</summary>
    public static IBrush ErrorBackground { get; } = Frozen(Color.Parse("#FDECEA"));

    /// <summary>Ogohlantirish foni.</summary>
    public static IBrush WarningBackground { get; } = Frozen(Color.Parse("#FFF4E0"));

    /// <summary>Tanlangan katak ramkasi.</summary>
    public static IBrush Selection { get; } = Frozen(Color.Parse("#1565C0"));

    /// <summary>Oddiy katak ramkasi.</summary>
    public static IBrush CellBorder { get; } = Frozen(Color.Parse("#D6D9E0"));

    /// <summary>"#RRGGBB" rang kodining ochiq tonini qaytaradi.</summary>
    public static IBrush Light(string? colorCode)
    {
        if (string.IsNullOrWhiteSpace(colorCode))
        {
            return Fallback;
        }

        lock (Cache)
        {
            if (Cache.TryGetValue(colorCode, out var cached))
            {
                return cached;
            }

            if (!Color.TryParse(colorCode, out var color))
            {
                return Fallback;
            }

            var brush = Frozen(Lighten(color, 0.72));
            Cache[colorCode] = brush;
            return brush;
        }
    }

    private static Color Lighten(Color color, double amount)
    {
        byte Mix(byte channel) => (byte)Math.Clamp(channel + ((255 - channel) * amount), 0, 255);
        return Color.FromRgb(Mix(color.R), Mix(color.G), Mix(color.B));
    }

    private static IBrush Frozen(Color color) => new ImmutableSolidColorBrush(color);
}
