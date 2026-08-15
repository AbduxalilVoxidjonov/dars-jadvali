namespace DarsJadvali.Infrastructure.Persistence.Projection;

/// <summary>
/// <b>Eski nom — moslik uchun.</b> Haqiqiy kontrakt endi
/// <see cref="DarsJadvali.Application.Abstractions.ICardOccurrenceProjector"/> da
/// (00 §10.8 TODO-2 bajarildi); implementatsiya shu yerda qoldi.
/// </summary>
/// <remarks>
/// Bu interfeys faqat <c>DarsJadvali.Infrastructure.Persistence.Projection</c> nomlar
/// fazosiga tayangan mavjud chaqiruvchilar (jumladan <c>SchemaV2</c> testlari va
/// <c>LegacyToV2Backfill</c>) sinmasligi uchun saqlanadi — yangi a'zo qo'shilmaydi.
/// </remarks>
public interface ICardOccurrenceProjector : DarsJadvali.Application.Abstractions.ICardOccurrenceProjector
{
}
