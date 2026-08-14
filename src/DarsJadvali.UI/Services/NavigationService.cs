using DarsJadvali.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DarsJadvali.UI.Services;

/// <summary>
/// Har bir sahifani alohida DI qamrovi (scope) ichida yaratadi.
/// Shu tufayli har sahifa yangi DbContext bilan ishlaydi va eski ma'lumot qolib ketmaydi.
/// </summary>
public sealed class NavigationService : INavigationService, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private IServiceScope? _currentScope;
    private bool _disposed;

    public NavigationService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    public ViewModelBase? Current { get; private set; }

    public event EventHandler<ViewModelBase>? Navigated;

    public ViewModelBase NavigateTo<TViewModel>() where TViewModel : ViewModelBase
        => NavigateTo(typeof(TViewModel));

    public ViewModelBase NavigateTo(Type viewModelType)
    {
        ArgumentNullException.ThrowIfNull(viewModelType);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var scope = _scopeFactory.CreateScope();
        ViewModelBase viewModel;

        try
        {
            viewModel = (ViewModelBase)scope.ServiceProvider.GetRequiredService(viewModelType);
        }
        catch
        {
            scope.Dispose();
            throw;
        }

        var previousScope = _currentScope;
        _currentScope = scope;
        Current = viewModel;

        Navigated?.Invoke(this, viewModel);

        previousScope?.Dispose();
        return viewModel;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _currentScope?.Dispose();
        _currentScope = null;
        Current = null;
    }
}
