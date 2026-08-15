namespace DarsJadvali.Application.Validation;

/// <summary>
/// Dars joylashtirish siyosatining <b>yagona manbasi</b>: topilgan konfliktlar bilan
/// joylashtirish mumkinmi yoki yo'q.
/// </summary>
/// <remarks>
/// 05-audit K-06: ilgari generator <c>Warning</c> li slotni zaxira variant sifatida
/// qabul qilar, <c>ScheduleService.PlaceAsync</c> esa xuddi shu slotni rad etardi.
/// Natijada generator yaratgan darsni foydalanuvchi keyin bir katak ham surolmasdi.
/// Endi ikkala yo'l ham shu yagona qoidaga tayanadi.
/// <para>
/// Qoida: <c>Error</c> har doim to'sadi; <c>Warning</c> faqat <c>force = true</c>
/// bo'lganda (foydalanuvchi ataylab tasdiqlaganda) o'tkaziladi.
/// </para>
/// </remarks>
public static class SchedulePlacementPolicy
{
    /// <summary>Konfliktlar ro'yxati bilan joylashtirish mumkinmi.</summary>
    public static bool IsAcceptable(IReadOnlyList<Conflict> conflicts, bool force = false)
    {
        ArgumentNullException.ThrowIfNull(conflicts);

        foreach (var conflict in conflicts)
        {
            if (conflict.Severity == ConflictSeverity.Error) return false;
            if (!force) return false;
        }

        return true;
    }

    /// <summary>Validatsiya natijasi bilan joylashtirish mumkinmi.</summary>
    public static bool IsAcceptable(ValidationResult validation, bool force = false)
    {
        ArgumentNullException.ThrowIfNull(validation);
        return validation.IsValid && (force || !validation.HasWarnings);
    }
}
