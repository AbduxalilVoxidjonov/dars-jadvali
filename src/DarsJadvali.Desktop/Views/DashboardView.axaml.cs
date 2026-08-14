using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DarsJadvali.Desktop.ViewModels;

namespace DarsJadvali.Desktop.Views;

/// <summary>
/// Bosh sahifa. Maktab jadvali bitta <see cref="Grid"/> ichida kod tomondan quriladi:
/// Avalonia'da <c>SharedSizeGroup</c> yo'q, shuning uchun ustun kengliklari qat'iy piksel
/// qiymatlar bilan beriladi va sinf nomi katagi <c>Grid.RowSpan</c> orqali cho'ziladi.
/// </summary>
public partial class DashboardView : UserControl
{
    private const double ClassColumnWidth = 136;
    private const double LessonColumnWidth = 104;
    private const double DayColumnWidth = 150;

    private DashboardViewModel? _viewModel;

    /// <summary>Ekranni yaratadi.</summary>
    public DashboardView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += (_, _) => BuildTimetable();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as DashboardViewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        BuildTimetable();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or nameof(DashboardViewModel.Timetable))
        {
            BuildTimetable();
        }
    }

    /// <summary>Maktab jadvalining sarlavha va tana to'rlarini qaytadan quradi.</summary>
    private void BuildTimetable()
    {
        var header = TimetableHeaderGrid;
        var body = TimetableBodyGrid;

        Clear(header);
        Clear(body);

        var model = _viewModel?.Timetable;

        if (model is null || model.IsEmpty)
        {
            return;
        }

        var dayCount = model.DayHeaders.Count;
        var columnCount = dayCount + 2;

        var lineBrush = Resource("AppBorderBrush", "#D6D9E0");
        var mutedBrush = Resource("AppMutedTextBrush", "#78909C");
        var headerBrush = Resource("AppHeaderBrush", "#EDE7F6");
        var surfaceBrush = Resource("AppSurfaceBrush", "#FFFFFF");
        var alternateBrush = new SolidColorBrush(Color.Parse("#F6F4FB"));

        AddColumns(header, dayCount);
        AddColumns(body, dayCount);

        // --- Sarlavha qatori (aylantirilganda joyida qoladi) ---
        header.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        Place(header, HeaderCell("SINF", headerBrush, lineBrush), 0, 0);
        Place(header, HeaderCell("SOAT", headerBrush, lineBrush), 0, 1);

        for (var i = 0; i < dayCount; i++)
        {
            Place(header, HeaderCell(model.DayHeaders[i], headerBrush, lineBrush), 0, i + 2);
        }

        // --- Sinf bloklari ---
        var totalRows = model.Blocks.Sum(b => b.Rows.Count);

        for (var i = 0; i < totalRows; i++)
        {
            body.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        var rowIndex = 0;

        foreach (var block in model.Blocks)
        {
            var span = block.Rows.Count;

            if (span == 0)
            {
                continue;
            }

            // Guruh foni + guruhlar orasidagi qalinroq chiziq.
            var background = new Border
            {
                Background = block.IsAlternate ? alternateBrush : surfaceBrush,
                BorderBrush = mutedBrush,
                BorderThickness = new Thickness(0, rowIndex == 0 ? 0 : 2, 0, 0),
            };

            Grid.SetRow(background, rowIndex);
            Grid.SetRowSpan(background, span);
            Grid.SetColumn(background, 0);
            Grid.SetColumnSpan(background, columnCount);
            body.Children.Add(background);

            // Sinf nomi — butun blok bo'ylab bitta katak (Grid.RowSpan).
            var classCell = ClassCell(block, lineBrush, mutedBrush);
            Grid.SetRow(classCell, rowIndex);
            Grid.SetRowSpan(classCell, span);
            Grid.SetColumn(classCell, 0);
            body.Children.Add(classCell);

            for (var r = 0; r < span; r++)
            {
                var row = block.Rows[r];

                Place(body, LessonCell(row, lineBrush, mutedBrush), rowIndex + r, 1);

                for (var d = 0; d < dayCount && d < row.Cells.Count; d++)
                {
                    Place(body, LessonBox(row.Cells[d], lineBrush, mutedBrush), rowIndex + r, d + 2);
                }
            }

            rowIndex += span;
        }
    }

    private static void Clear(Grid grid)
    {
        grid.Children.Clear();
        grid.RowDefinitions.Clear();
        grid.ColumnDefinitions.Clear();
    }

    private static void AddColumns(Grid grid, int dayCount)
    {
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(ClassColumnWidth)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(LessonColumnWidth)));

        for (var i = 0; i < dayCount; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(DayColumnWidth)));
        }
    }

    private static void Place(Grid grid, Control control, int row, int column)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        grid.Children.Add(control);
    }

    private static Border HeaderCell(string text, IBrush background, IBrush line) => new()
    {
        Background = background,
        BorderBrush = line,
        BorderThickness = new Thickness(0, 0, 1, 1),
        Padding = new Thickness(8),
        Child = new TextBlock
        {
            Text = text,
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        },
    };

    /// <summary>Sinf nomi katagi — "Tahrirlash" tugmasi bilan.</summary>
    private Border ClassCell(ClassTimetableViewModel block, IBrush line, IBrush muted)
    {
        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        panel.Children.Add(new TextBlock
        {
            Text = block.ClassName,
            FontSize = 16,
            FontWeight = FontWeight.Bold,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        if (block.HasRoomText)
        {
            panel.Children.Add(Small(block.RoomText, muted));
        }

        panel.Children.Add(Small(block.SummaryText, muted));

        var button = new Button
        {
            Content = "Tahrirlash",
            FontSize = 11,
            Padding = new Thickness(12, 2),
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            Command = _viewModel?.EditClassCommand,
            CommandParameter = block,
        };

        panel.Children.Add(button);

        return new Border
        {
            BorderBrush = line,
            BorderThickness = new Thickness(0, 0, 1, 1),
            Padding = new Thickness(8, 10),
            Child = panel,
        };
    }

    /// <summary>Soat ustuni: "3-soat" va ostida "10:20-11:05".</summary>
    private static Border LessonCell(ClassTimetableRowViewModel row, IBrush line, IBrush muted)
    {
        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        panel.Children.Add(new TextBlock
        {
            Text = row.LessonText,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        if (row.HasTimeText)
        {
            panel.Children.Add(Small(row.TimeText, muted));
        }

        return new Border
        {
            BorderBrush = line,
            BorderThickness = new Thickness(0, 0, 1, 1),
            Padding = new Thickness(6, 4),
            Child = panel,
        };
    }

    /// <summary>Kun katagi: fan (qalin) + o'qituvchi + xona, foni o'qituvchi rangining ochiq toni.</summary>
    private static Border LessonBox(DashboardCellViewModel cell, IBrush line, IBrush muted)
    {
        var border = new Border
        {
            Background = cell.Background,
            BorderBrush = line,
            BorderThickness = new Thickness(0, 0, 1, 1),
            Padding = new Thickness(6, 4),
            MinHeight = 48,
        };

        if (!cell.HasEntry)
        {
            return border;
        }

        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        panel.Children.Add(new TextBlock
        {
            Text = cell.SubjectName,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });

        panel.Children.Add(Small(cell.TeacherName, muted, HorizontalAlignment.Left));

        if (cell.HasRoom)
        {
            panel.Children.Add(Small(cell.RoomDisplayText, muted, HorizontalAlignment.Left));
        }

        border.Child = panel;
        return border;
    }

    private static TextBlock Small(
        string text,
        IBrush muted,
        HorizontalAlignment alignment = HorizontalAlignment.Center) => new()
    {
        Text = text,
        FontSize = 10,
        Foreground = muted,
        TextWrapping = TextWrapping.Wrap,
        HorizontalAlignment = alignment,
    };

    /// <summary>Resursdan brush oladi; topilmasa zaxira rangdan foydalanadi.</summary>
    private IBrush Resource(string key, string fallback)
        => this.TryFindResource(key, out var value) && value is IBrush brush
            ? brush
            : new SolidColorBrush(Color.Parse(fallback));
}
