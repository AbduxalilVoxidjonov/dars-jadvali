using Avalonia.Controls;
using Avalonia.Input;
using DarsJadvali.Desktop.ViewModels;

namespace DarsJadvali.Desktop.Views;

/// <summary>Dars jadvali ekrani (interaktiv to'r).</summary>
public partial class TimetableView : UserControl
{
    /// <summary>Ekranni yaratadi.</summary>
    public TimetableView()
    {
        InitializeComponent();
    }

    /// <summary>Katak sichqonchaning chap tugmasi bilan bosilganda uni tanlaydi.</summary>
    private void OnCellPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: TimetableCellViewModel cell })
        {
            return;
        }

        if (!e.GetCurrentPoint(sender as Control).Properties.IsLeftButtonPressed)
        {
            return;
        }

        cell.SelectCommand.Execute(null);
    }
}
