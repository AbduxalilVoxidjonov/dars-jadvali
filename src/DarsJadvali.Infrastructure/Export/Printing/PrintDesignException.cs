namespace DarsJadvali.Infrastructure.Export.Printing;

/// <summary>
/// Dizayn ta'rifi noto'g'ri. Xabar HAR DOIM foydalanuvchi tuzatishi mumkin bo'lgan
/// aniq joyni ko'rsatadi: qaysi element, qaysi maydon, nima kutilgan edi.
/// </summary>
public sealed class PrintDesignException : Exception
{
    /// <summary>Xatolik joyi, masalan <c>elements[2].rect</c>.</summary>
    public string Path { get; }

    /// <summary>Yangi xato.</summary>
    /// <param name="path">Xatolik yo'li (JSON ichidagi joy).</param>
    /// <param name="message">Nima noto'g'ri va nima kutilgani.</param>
    /// <param name="inner">Ichki xato (masalan JSON sintaksis xatosi).</param>
    public PrintDesignException(string path, string message, Exception? inner = null)
        : base(string.IsNullOrEmpty(path) ? message : $"Dizayn xatosi ({path}): {message}", inner)
    {
        Path = path ?? string.Empty;
    }
}
