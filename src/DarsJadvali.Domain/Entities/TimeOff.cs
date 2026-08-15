using DarsJadvali.Domain.Common;
using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Domain.Entities;

/// <summary>
/// Vaqt cheklovi matritsasi — aSc'dagi "time-off" ning aynan ko'chirmasi.
/// </summary>
/// <remarks>
/// Eski <see cref="TeacherAvailability"/> dan ikkita muhim farqi bor:
/// <list type="number">
/// <item><b>Uch holatli</b> (<see cref="AvailabilityLevel"/>): ruxsat / tavsiya
/// etilmaydi ("?", jarimali) / taqiqlangan. Eski model faqat ikki holatli edi.</item>
/// <item><b>Vaqt oralig'i emas, (kun, soat) katakchasi</b> — ustma-ust tushuvchi
/// oraliqlar endi imkonsiz.</item>
/// </list>
/// Egasi faqat o'qituvchi emas: guruh, sinf, xona, fan yoki butun maktab ham bo'la oladi
/// (<see cref="OwnerKind"/>).
/// <para>1-bosqichda eski <see cref="TeacherAvailability"/> BUZILMAYDI — unga ko'chirish
/// keyingi bosqichda.</para>
/// </remarks>
public class TimeOff : BaseEntity, IConcurrencyAware
{
    /// <summary>O'quv yili Id.</summary>
    public int AcademicYearId { get; set; }

    /// <summary>O'quv yili.</summary>
    public AcademicYear? AcademicYear { get; set; }

    /// <summary>Cheklov kimga tegishli.</summary>
    public ResourceOwnerKind OwnerKind { get; set; }

    /// <summary>Egasining Id'si (<see cref="OwnerKind"/> ga qarab). <c>Global</c> uchun 0.</summary>
    public int OwnerId { get; set; }

    /// <summary>Kun raqami, 0-based.</summary>
    public int DayNo { get; set; }

    /// <summary>Dars soati raqami.</summary>
    public int PeriodNo { get; set; }

    /// <summary>Qaysi haftalarga tegishli. <c>0</c> = barcha haftalar.</summary>
    public int WeeksMask { get; set; }

    /// <summary>Ruxsat darajasi.</summary>
    public AvailabilityLevel Availability { get; set; } = AvailabilityLevel.Allowed;

    /// <summary>
    /// <see cref="AvailabilityLevel.NotRecommended"/> uchun jarima og'irligi (0..1000).
    /// Taqiqlangan/ruxsat etilgan holatlarda ishlatilmaydi.
    /// </summary>
    /// <remarks>
    /// <b>Yadroga qanday yetadi.</b> <c>DarsJadvali.Scheduling</c> yadrosida "?" holati
    /// bitta bitmask (<c>Card.QuestionMarked</c>) va QAT'IY og'irlik (C-AVL-06, w=100) —
    /// ya'ni qator bo'yicha turli og'irlikni ifodalab bo'lmaydi. Shu sababli
    /// <c>SchedulingMapper</c> jarimani faqat DARAJA tanlashda ishlatadi:
    /// <see cref="HardThreshold"/> dan katta-teng jarima amalda taqiq deb qabul qilinadi
    /// (<c>Forbidden</c>), qolgan barcha musbat qiymatlar bitta "?" og'irligiga tushadi.
    /// </remarks>
    public int Penalty { get; set; }

    /// <summary>
    /// Jarima shu qiymatga yetsa cheklov amalda TAQIQ deb qaraladi
    /// (<c>CK_TimeOffs_Penalty</c> ning yuqori chegarasi bilan bir xil).
    /// </summary>
    public const int HardThreshold = 1000;

    /// <summary>
    /// Eski <see cref="TeacherAvailability"/> Id — <c>V2_06</c> ko'chirish izi.
    /// Bitta eski oraliq bir nechta katakchaga yoyilgani uchun unikal EMAS.
    /// </summary>
    public int? LegacyTeacherAvailabilityId { get; set; }
}
