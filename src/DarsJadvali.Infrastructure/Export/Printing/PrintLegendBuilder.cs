using System.Globalization;

namespace DarsJadvali.Infrastructure.Export.Printing;

/// <summary>Legendaning bitta yozuvi.</summary>
/// <param name="Text">Asosiy matn (fan/o'qituvchi/xona nomi).</param>
/// <param name="Detail">O'ng tomondagi qo'shimcha: soat soni yoki qisqartma.</param>
/// <param name="Color">Rang namunasi "#RRGGBB" (bo'lmasa <c>null</c>).</param>
public sealed record PrintLegendItem(string Text, string? Detail, string? Color);

/// <summary>
/// Kartalardan legenda yozuvlarini yig'adi (aSc <c>m_LegendaType</c> ekvivalenti).
/// </summary>
public static class PrintLegendBuilder
{
    /// <summary>Legenda turi bo'yicha standart sarlavha.</summary>
    /// <param name="kind">Legenda turi.</param>
    public static string DefaultTitle(PrintLegendKind kind) => kind switch
    {
        PrintLegendKind.Subjects => "Fanlar",
        PrintLegendKind.Teachers => "O'qituvchilar",
        PrintLegendKind.Rooms => "Xonalar",
        _ => "Darslar",
    };

    /// <summary>Berilgan to'rlardagi kartalardan legenda yozuvlarini quradi.</summary>
    /// <param name="cards">Kartalar.</param>
    /// <param name="kind">Legenda turi.</param>
    /// <param name="colorBy">Rang manbai (rang namunasi uchun).</param>
    /// <param name="maxItems">Eng ko'p yozuv soni.</param>
    public static IReadOnlyList<PrintLegendItem> Build(
        IEnumerable<PrintableCard> cards,
        PrintLegendKind kind,
        PrintColorSource colorBy = PrintColorSource.Card,
        int maxItems = 60)
    {
        ArgumentNullException.ThrowIfNull(cards);

        var list = cards as IReadOnlyList<PrintableCard> ?? cards.ToList();

        var items = kind switch
        {
            PrintLegendKind.Subjects => BuildSubjects(list, colorBy),
            PrintLegendKind.Teachers => BuildTeachers(list),
            PrintLegendKind.Rooms => BuildRooms(list),
            _ => BuildLessons(list, colorBy),
        };

        return maxItems > 0 && items.Count > maxItems
            ? items.Take(maxItems).ToList()
            : items;
    }

    private static List<PrintLegendItem> BuildSubjects(IReadOnlyList<PrintableCard> cards, PrintColorSource colorBy)
    {
        var groups = new Dictionary<string, (string? Short, int Hours, string? Color)>(StringComparer.OrdinalIgnoreCase);

        foreach (var card in cards)
        {
            if (string.IsNullOrWhiteSpace(card.SubjectName))
                continue;

            var key = card.SubjectName.Trim();
            groups.TryGetValue(key, out var current);
            groups[key] = (
                current.Short ?? (string.IsNullOrWhiteSpace(card.SubjectShortName) ? null : card.SubjectShortName),
                current.Hours + Math.Max(1, card.Length),
                current.Color ?? ResolveColor(card, colorBy));
        }

        return groups
            .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
            .Select(p => new PrintLegendItem(
                p.Key,
                p.Value.Short is null
                    ? $"{p.Value.Hours.ToString(CultureInfo.InvariantCulture)} soat"
                    : $"{p.Value.Short} · {p.Value.Hours.ToString(CultureInfo.InvariantCulture)} soat",
                p.Value.Color))
            .ToList();
    }

    private static List<PrintLegendItem> BuildTeachers(IReadOnlyList<PrintableCard> cards)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var card in cards)
        {
            foreach (var teacher in card.TeacherNames)
            {
                if (string.IsNullOrWhiteSpace(teacher))
                    continue;

                var key = teacher.Trim();
                counts[key] = counts.TryGetValue(key, out var value) ? value + Math.Max(1, card.Length) : Math.Max(1, card.Length);
            }
        }

        return counts
            .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
            .Select(p => new PrintLegendItem(
                p.Key,
                $"{p.Value.ToString(CultureInfo.InvariantCulture)} soat",
                PrintColor.FromName(p.Key)))
            .ToList();
    }

    private static List<PrintLegendItem> BuildRooms(IReadOnlyList<PrintableCard> cards)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var card in cards)
        {
            if (string.IsNullOrWhiteSpace(card.RoomName))
                continue;

            var key = card.RoomName!.Trim();
            counts[key] = counts.TryGetValue(key, out var value) ? value + Math.Max(1, card.Length) : Math.Max(1, card.Length);
        }

        return counts
            .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
            .Select(p => new PrintLegendItem(
                p.Key,
                $"{p.Value.ToString(CultureInfo.InvariantCulture)} soat",
                PrintColor.FromName(p.Key)))
            .ToList();
    }

    /// <summary>
    /// "Lessons table" (aSc <c>m_LegendaType=8</c>): fan + guruh + o'qituvchi + haftalik soat.
    /// </summary>
    private static List<PrintLegendItem> BuildLessons(IReadOnlyList<PrintableCard> cards, PrintColorSource colorBy)
    {
        var groups = new Dictionary<(string Subject, string Group, string Teacher), (int Hours, string? Color)>();

        foreach (var card in cards)
        {
            if (string.IsNullOrWhiteSpace(card.SubjectName))
                continue;

            var key = (
                card.SubjectName.Trim(),
                card.GroupName?.Trim() ?? string.Empty,
                card.TeacherLine);

            groups.TryGetValue(key, out var current);
            groups[key] = (current.Hours + Math.Max(1, card.Length), current.Color ?? ResolveColor(card, colorBy));
        }

        return groups
            .OrderBy(p => p.Key.Subject, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Key.Group, StringComparer.OrdinalIgnoreCase)
            .Select(p =>
            {
                var text = p.Key.Group.Length == 0
                    ? p.Key.Subject
                    : $"{p.Key.Subject} ({p.Key.Group})";

                var detail = p.Key.Teacher.Length == 0
                    ? $"{p.Value.Hours.ToString(CultureInfo.InvariantCulture)} soat"
                    : $"{p.Key.Teacher} · {p.Value.Hours.ToString(CultureInfo.InvariantCulture)} soat";

                return new PrintLegendItem(text, detail, p.Value.Color);
            })
            .ToList();
    }

    private static string? ResolveColor(PrintableCard card, PrintColorSource colorBy) => colorBy switch
    {
        PrintColorSource.None => null,
        PrintColorSource.Subject => PrintColor.FromName(card.SubjectName),
        _ => string.IsNullOrWhiteSpace(card.ColorCode) ? PrintColor.FromName(card.SubjectName) : card.ColorCode,
    };
}
