using DarsJadvali.Application.Abstractions;

namespace DarsJadvali.Infrastructure.Persistence;

/// <summary>
/// <b>Eski nom — moslik uchun.</b> Haqiqiy kontrakt endi
/// <see cref="DarsJadvali.Application.Abstractions.ITransactionalUnitOfWork"/> da
/// (00 §10.8 TODO-2 bajarildi) va <see cref="IUnitOfWork"/> undan meros oladi.
/// </summary>
/// <remarks>
/// Bu interfeys faqat <c>DarsJadvali.Infrastructure.Persistence</c> nomlar fazosiga
/// tayangan mavjud chaqiruvchilar (jumladan <c>SchemaV2</c> testlari) sinmasligi uchun
/// saqlanadi — yangi a'zo qo'shilmaydi. Yangi kodda <see cref="IUnitOfWork"/> yoki
/// <see cref="DarsJadvali.Application.Abstractions.ITransactionalUnitOfWork"/> ishlatiladi.
/// </remarks>
public interface ITransactionalUnitOfWork : IUnitOfWork
{
}
