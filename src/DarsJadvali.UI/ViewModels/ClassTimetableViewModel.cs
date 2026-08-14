using System.Collections.ObjectModel;

namespace DarsJadvali.UI.ViewModels;

/// <summary>Bosh sahifadagi sinf filtri bandi ("Barcha sinflar" uchun Id = 0).</summary>
/// <param name="Id">Sinf identifikatori; 0 bo'lsa — barcha sinflar.</param>
/// <param name="Name">Ro'yxatda ko'rinadigan nom.</param>
public sealed record ClassFilterOption(int Id, string Name);

/// <summary>Umumiy maktab jadvalidagi bitta sinf guruhi (bir nechta soat qatoridan iborat).</summary>
public sealed class ClassTimetableViewModel
{
    /// <summary>Sinf identifikatori.</summary>
    public int ClassGroupId { get; init; }

    /// <summary>Sinf nomi ("5-A").</summary>
    public string ClassName { get; init; } = string.Empty;

    /// <summary>Sinfning asosiy xonasi ("101-xona"), bo'sh bo'lishi mumkin.</summary>
    public string RoomText { get; init; } = string.Empty;

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

    /// <summary>Faol ish kunlari soni — kataklar shuncha ustunga bo'linadi.</summary>
    public int DayCount { get; init; } = 1;

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

    /// <summary>Katak foni uchun o'qituvchi rang kodi.</summary>
    public string ColorCode { get; init; } = "#FFFFFF";

    /// <summary>Katakda dars bormi.</summary>
    public bool HasEntry { get; init; }
}
