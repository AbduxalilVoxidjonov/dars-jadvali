using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using DarsJadvali.Desktop.Models;
using DarsJadvali.Desktop.ViewModels;

namespace DarsJadvali.Desktop.Views;

/// <summary>
/// Jadval to'rining code-behind'i — <b>faqat hodisalarni uzatadi</b>.
/// </summary>
/// <remarks>
/// <para>
/// M-04 dagi 319 qatorlik imperativ to'r qurish butunlay olib tashlandi: bu yerda birorta
/// <c>Border</c> yoki <c>TextBlock</c> yaratilmaydi. To'r <c>TimetableBoardView.axaml</c> da
/// deklarativ, kataklar esa <c>VirtualizingStackPanel</c> tomonidan kerak bo'lgandagina quriladi.
/// </para>
/// <para>
/// Bu yerda faqat sichqoncha va klaviatura hodisalari
/// <see cref="TimetableBoardViewModel"/> ning mantiqiy metodlariga uzatiladi —
/// shuning uchun mantiqni XAML'siz sinovdan o'tkazish mumkin.
/// </para>
/// </remarks>
public partial class TimetableBoardView : UserControl
{
    private TopLevel? _topLevel;

    /// <summary>Ekranni yaratadi.</summary>
    public TimetableBoardView()
    {
        InitializeComponent();

        AttachedToVisualTree += OnAttached;
        DetachedFromVisualTree += OnDetached;

        AddHandler(PointerMovedEvent, OnPointerMovedHandler, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, OnPointerPressedHandler, RoutingStrategies.Tunnel);
        AddHandler(PointerExitedEvent, OnPointerExitedHandler, RoutingStrategies.Tunnel);
    }

    private TimetableBoardViewModel? ViewModel => DataContext as TimetableBoardViewModel;

    // ================= Klaviatura =================

    private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _topLevel = TopLevel.GetTopLevel(this);

        if (_topLevel is null)
        {
            return;
        }

        _topLevel.AddHandler(KeyDownEvent, OnKeyDownHandler, RoutingStrategies.Tunnel);
        _topLevel.AddHandler(KeyUpEvent, OnKeyUpHandler, RoutingStrategies.Tunnel);
    }

    private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_topLevel is null)
        {
            return;
        }

        _topLevel.RemoveHandler(KeyDownEvent, OnKeyDownHandler);
        _topLevel.RemoveHandler(KeyUpEvent, OnKeyUpHandler);
        _topLevel = null;
    }

    /// <summary>aSc klaviatura qisqartmalari (03-asc-features-ux.md §4.4).</summary>
    private void OnKeyDownHandler(object? sender, KeyEventArgs e)
    {
        var vm = ViewModel;
        if (vm is null || !IsEffectivelyVisible)
        {
            return;
        }

        // SHIFT — mumkin pozitsiyalarni yoritish.
        if (e.Key is Key.LeftShift or Key.RightShift)
        {
            vm.SetHighlighting(true);
            return;
        }

        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        switch (e.Key)
        {
            case Key.Escape:
                vm.CancelDragCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Z when ctrl:
                vm.UndoCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Y when ctrl:
                vm.RedoCommand.Execute(null);
                e.Handled = true;
                break;

            // Ko'p klaviaturada Redo = Ctrl+Shift+Z.
            case Key.Z when ctrl && e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                vm.RedoCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.OemPlus or Key.Add:
                vm.ZoomInCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.OemMinus or Key.Subtract:
                vm.ZoomOutCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Multiply:
                vm.ToggleInvertCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.D0 when ctrl:
                vm.ZoomResetCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void OnKeyUpHandler(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.LeftShift or Key.RightShift)
        {
            ViewModel?.SetHighlighting(false);
        }
    }

    // ================= Sichqoncha =================

    private void OnPointerMovedHandler(object? sender, PointerEventArgs e)
    {
        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        var slot = FindDataContext<TimetableSlotViewModel>(e.Source);

        // Faqat katak o'zgarganda ishlaymiz — har piksel harakatida emas.
        vm.HoverSlot(slot);
    }

    private void OnPointerExitedHandler(object? sender, PointerEventArgs e)
    {
        if (FindDataContext<TimetableSlotViewModel>(e.Source) is null)
        {
            ViewModel?.HoverSlot(null);
        }
    }

    private void OnPointerPressedHandler(object? sender, PointerPressedEventArgs e)
    {
        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        Focus();

        var point = e.GetCurrentPoint(this);
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        // Joylashtirilmagan kartalar panelidan olish.
        if (FindDataContext<TimetableCard>(e.Source) is { } unplaced)
        {
            if (point.Properties.IsLeftButtonPressed)
            {
                vm.PickUp(unplaced, ctrl);
                e.Handled = true;
            }

            return;
        }

        var slot = FindDataContext<TimetableSlotViewModel>(e.Source);
        if (slot is null)
        {
            // To'rdan tashqariga bosildi — kartani qo'ldan qo'yib yuboramiz (aSc §4.1).
            if (vm.HasCardInHand)
            {
                vm.CancelDragCommand.Execute(null);
            }

            return;
        }

        if (point.Properties.IsRightButtonPressed)
        {
            ShowContextMenu(vm, slot, e);
            e.Handled = true;
            return;
        }

        if (point.Properties.IsLeftButtonPressed)
        {
            vm.ClickSlot(slot, ctrl);
            e.Handled = true;
        }
    }

    /// <summary>
    /// O'ng tugma menyusi: karta ustida — qulflash/o'chirish, bo'sh katakda — teskari qidiruv
    /// (shu joyga mos kartalar ro'yxati, aSc §4.3).
    /// </summary>
    private static void ShowContextMenu(
        TimetableBoardViewModel vm, TimetableSlotViewModel slot, PointerPressedEventArgs e)
    {
        var menu = new MenuFlyout();

        if (slot.Card is { } card)
        {
            menu.Items.Add(new MenuItem
            {
                Header = card.IsLocked ? "Qulfni ochish" : "Qulflash",
                Command = vm.ToggleLockCommand,
                CommandParameter = card,
            });

            menu.Items.Add(new MenuItem
            {
                Header = "Panelga olib qo'yish",
                Command = vm.UnplaceCardCommand,
                CommandParameter = card,
            });
        }
        else
        {
            var candidates = vm.CandidatesFor(slot);

            if (candidates.Count == 0)
            {
                menu.Items.Add(new MenuItem { Header = "Bu joyga mos karta yo'q", IsEnabled = false });
            }
            else
            {
                foreach (var candidate in candidates.Take(20))
                {
                    var item = new MenuItem
                    {
                        Header = $"{candidate.SubjectName} — {candidate.ScopeText}",
                    };

                    var target = candidate;
                    item.Click += (_, _) =>
                    {
                        if (vm.PickUp(target))
                        {
                            vm.DropAt(slot.Day, slot.Period);
                        }
                    };

                    menu.Items.Add(item);
                }
            }
        }

        if (e.Source is Control control)
        {
            menu.ShowAt(control, true);
        }
    }

    /// <summary>Zichlik almashtirgichi.</summary>
    private void OnDensityChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is null || sender is not ComboBox combo)
        {
            return;
        }

        var density = combo.SelectedIndex switch
        {
            0 => TimetableDensity.Zich,
            2 => TimetableDensity.Keng,
            _ => TimetableDensity.Oddiy,
        };

        ViewModel.SetDensityCommand.Execute(density);
    }

    /// <summary>Hodisa manbasidan yuqoriga qarab kerakli DataContext'ni topadi.</summary>
    private static T? FindDataContext<T>(object? source)
        where T : class
    {
        var current = source as Visual;

        while (current is not null)
        {
            if (current is StyledElement styled && styled.DataContext is T match)
            {
                return match;
            }

            current = current.GetVisualParent();
        }

        return null;
    }
}
