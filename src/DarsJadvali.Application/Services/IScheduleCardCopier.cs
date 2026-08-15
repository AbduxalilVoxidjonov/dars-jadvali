namespace DarsJadvali.Application.Services;

/// <summary>
/// Jadval varianti nusxalanganda uning <b>kartochkalarini</b> (v2 modeli) ham ko'chiradi.
/// </summary>
/// <remarks>
/// <b>Nima uchun alohida interfeys.</b> <see cref="ScheduleSetService"/> Application
/// qatlamida va u faqat <c>IUnitOfWork</c> ni biladi, <c>IUnitOfWork</c> da esa
/// <c>Card</c> repozitoriysi YO'Q (kartochka yo'li ataylab <c>ISchedulingStore</c> /
/// proyektor orqali ketadi). Nusxalash mantiqini Infrastructure'da qoldirib, bu yerga
/// faqat shartnomani chiqaramiz.
/// <para>
/// <b>Nima uchun umuman kerak.</b> <see cref="IScheduleSetService.DuplicateAsync"/>
/// ilgari faqat eski <c>ScheduleEntry</c> qatorlarini nusxalardi. Ko'chirish (backfill)
/// bajarilgan haqiqiy bazada bu <b>jimgina yo'qotish</b> edi: nusxada eski yozuvlar bor,
/// lekin <c>/api/board</c> va Desktop taxtasi o'qiydigan kartochkalar YO'Q — ya'ni
/// yangi variant bo'sh ko'rinardi.
/// </para>
/// </remarks>
public interface IScheduleCardCopier
{
    /// <summary>
    /// Manba jadvalning barcha kartochkalarini (va ularga tayinlangan xonalarni)
    /// nishon jadvalga nusxalaydi, so'ng bandlik proyeksiyasini qayta quradi.
    /// </summary>
    /// <remarks>
    /// <b>Idempotent:</b> nishon jadvalda allaqachon kartochka bo'lsa hech narsa
    /// yozilmaydi (<c>0</c> qaytadi) — takroriy chaqiruv dublikat yaratmaydi.
    /// <para>
    /// <c>LegacyScheduleEntryId</c> nusxaga KO'CHIRILMAYDI: u ko'chirish izi va uning
    /// ustida filtrlangan unikal indeks bor, nusxada takrorlansa indeks buzilardi.
    /// </para>
    /// </remarks>
    /// <param name="sourceScheduleId">Nusxalanadigan jadval.</param>
    /// <param name="targetScheduleId">Nusxa (allaqachon yaratilgan bo'lishi shart).</param>
    /// <param name="ct">Bekor qilish tokeni.</param>
    /// <returns>Nusxalangan kartochkalar soni.</returns>
    Task<int> CopyCardsAsync(
        int sourceScheduleId, int targetScheduleId, CancellationToken ct = default);
}
