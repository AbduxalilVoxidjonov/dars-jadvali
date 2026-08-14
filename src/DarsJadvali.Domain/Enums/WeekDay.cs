namespace DarsJadvali.Domain.Enums;

/// <summary>Hafta kunlari.</summary>
public enum WeekDay
{
    /// <summary>Dushanba.</summary>
    Dushanba = 1,

    /// <summary>Seshanba.</summary>
    Seshanba = 2,

    /// <summary>Chorshanba.</summary>
    Chorshanba = 3,

    /// <summary>Payshanba.</summary>
    Payshanba = 4,

    /// <summary>Juma.</summary>
    Juma = 5,

    /// <summary>Shanba.</summary>
    Shanba = 6,

    /// <summary>Yakshanba.</summary>
    Yakshanba = 7
}

/// <summary><see cref="WeekDay"/> uchun yordamchi metodlar.</summary>
public static class WeekDayExtensions
{
    private static readonly WeekDay[] AllDays =
    {
        WeekDay.Dushanba,
        WeekDay.Seshanba,
        WeekDay.Chorshanba,
        WeekDay.Payshanba,
        WeekDay.Juma,
        WeekDay.Shanba,
        WeekDay.Yakshanba
    };

    /// <summary>Kun nomini o'zbekcha qaytaradi.</summary>
    public static string ToUzbek(this WeekDay day) => day switch
    {
        WeekDay.Dushanba => "Dushanba",
        WeekDay.Seshanba => "Seshanba",
        WeekDay.Chorshanba => "Chorshanba",
        WeekDay.Payshanba => "Payshanba",
        WeekDay.Juma => "Juma",
        WeekDay.Shanba => "Shanba",
        WeekDay.Yakshanba => "Yakshanba",
        _ => day.ToString()
    };

    /// <summary>Barcha kunlar 1..7 tartibda.</summary>
    public static IReadOnlyList<WeekDay> All => AllDays;
}
