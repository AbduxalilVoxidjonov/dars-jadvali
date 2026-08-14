namespace DarsJadvali.Desktop.Models;

/// <summary>Chap menyudagi bitta band.</summary>
/// <param name="Title">Ko'rinadigan nom.</param>
/// <param name="IconKind">Belgi nomi (hozircha chizilmaydi, kelajak uchun).</param>
/// <param name="ViewModelType">Ochiladigan sahifa ViewModel turi.</param>
public sealed record MenuItemModel(string Title, string IconKind, Type ViewModelType);
