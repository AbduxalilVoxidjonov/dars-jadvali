namespace DarsJadvali.Desktop.Services;

/// <summary>
/// Bitta DI qamrovi (scope) ichidagi asinxron amallarni <b>ketma-ket</b> bajaradi.
/// </summary>
/// <remarks>
/// Sabab: <c>Application</c> va <c>Infrastructure</c> servislari <c>Scoped</c> bo'lgani uchun
/// bitta sahifa ViewModel'ining barcha amallari <b>bitta</b> <c>DbContext</c> ustida ishlaydi.
/// Ikkita amal kesishsa EF Core "A second operation was started on this context instance"
/// xatosini beradi (masalan rejim almashtirilganda ikkita <c>RefreshGrid</c> birdaniga ketadi).
/// <para>
/// Qoida: yangi amal kelganda <b>oldingisi bekor qilinadi</b> va uning tugashi kutiladi —
/// shunda foydalanuvchi tez-tez almashtirsa ham oxirgi tanlov g'olib chiqadi.
/// </para>
/// <para>
/// Amal <i>ichidan</i> qayta chaqirilsa (masalan "Qo'yish" oxirida to'rni yangilash),
/// navbat kutilmaydi — o'sha amalning tokeni bilan darhol bajariladi (o'zini o'zi bloklamasligi uchun).
/// </para>
/// </remarks>
public sealed class AsyncOperationRunner : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _sync = new();

    /// <summary>Joriy amal ichidamizmi — shu bo'lsa qiymatda uning tokeni turadi.</summary>
    private readonly AsyncLocal<StrongToken?> _ambient = new();

    private CancellationTokenSource? _current;
    private bool _disposed;

    /// <summary>Hozir biror amal navbatda yoki bajarilyaptimi.</summary>
    public bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                return _current is not null;
            }
        }
    }

    /// <summary>
    /// Amalni navbat bilan bajaradi: avval oldingi amal bekor qilinadi va tugashi kutiladi,
    /// keyin <paramref name="operation"/> ishga tushadi.
    /// </summary>
    /// <param name="operation">Bajariladigan amal; unga navbatning tokeni beriladi.</param>
    /// <param name="ct">Tashqi bekor qilish tokeni (ixtiyoriy).</param>
    public async Task RunAsync(Func<CancellationToken, Task> operation, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Amal ichidan chaqirildi — navbatni kutmaymiz, aks holda deadlock bo'ladi.
        if (_ambient.Value is StrongToken ambient)
        {
            await operation(ambient.Token).ConfigureAwait(true);
            return;
        }

        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        CancellationTokenSource? previous;

        lock (_sync)
        {
            previous = _current;
            _current = cts;
        }

        CancelQuietly(previous);

        try
        {
            await _gate.WaitAsync(cts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Navbatda turganimizda bizni ham almashtirishdi — jimgina chiqamiz.
            Finish(cts);
            return;
        }

        try
        {
            if (cts.IsCancellationRequested)
            {
                return;
            }

            _ambient.Value = new StrongToken(cts.Token);
            await operation(cts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // Bekor qilingan — e'tiborsiz.
        }
        finally
        {
            _ambient.Value = null;
            _gate.Release();
            Finish(cts);
        }
    }

    /// <summary>
    /// Joriy amalni bekor qiladi va <b>tugashini kutadi</b>.
    /// Sahifadan chiqishdan oldin chaqiriladi: eski sahifa o'z DI qamrovi yopilgunicha ishini tugatsin.
    /// </summary>
    public async Task CancelAndWaitAsync(CancellationToken ct = default)
    {
        if (_disposed)
        {
            return;
        }

        CancellationTokenSource? previous;

        lock (_sync)
        {
            previous = _current;
        }

        CancelQuietly(previous);

        if (previous is null)
        {
            return;
        }

        try
        {
            await _gate.WaitAsync(ct).ConfigureAwait(true);
            _gate.Release();
        }
        catch (OperationCanceledException)
        {
            // Kutish bekor qilindi — boshqa chora yo'q.
        }
        catch (ObjectDisposedException)
        {
            // Runner yopilgan.
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        CancellationTokenSource? current;
        lock (_sync)
        {
            current = _current;
            _current = null;
        }

        CancelQuietly(current);
        _gate.Dispose();
    }

    private void Finish(CancellationTokenSource cts)
    {
        lock (_sync)
        {
            if (ReferenceEquals(_current, cts))
            {
                _current = null;
            }
        }

        cts.Dispose();
    }

    private static void CancelQuietly(CancellationTokenSource? source)
    {
        try
        {
            source?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Allaqachon tugagan — bekor qilishning hojati yo'q.
        }
    }

    /// <summary>
    /// <see cref="AsyncLocal{T}"/> ichida <c>CancellationToken?</c> saqlash uchun o'ram
    /// (struct'ning nullable holati bilan chalkashmaslik uchun alohida sinf).
    /// </summary>
    private sealed class StrongToken
    {
        public StrongToken(CancellationToken token) => Token = token;

        public CancellationToken Token { get; }
    }
}
