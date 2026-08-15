using System.Globalization;
using System.Text.Json;

namespace DarsJadvali.Infrastructure.Export.Printing;

/// <summary>
/// Dizayn ta'rifini JSON dan o'qiydi.
/// </summary>
/// <remarks>
/// <para><b>Nega JSON (XML emas)?</b></para>
/// <list type="number">
///   <item>aSc'ning <c>def.xml</c> i aslida XML EMAS: ildiz tegi <c>&lt;&gt;</c> (nomsiz),
///     bir xil maydon ba'zan atribut, ba'zan bola element bo'lib yoziladi, ichida MFC
///     ning initsializatsiya qilinmagan xotira markeri (<c>-842150451</c>) uchraydi.
///     Ya'ni "XML" u yerda hech qanday foyda bermagan — takrorlashning ma'nosi yo'q.</item>
///   <item><c>System.Text.Json</c> .NET 8 tarkibida — YANGI PAKET KERAK EMAS.</item>
///   <item>Kelajakdagi vizual dizayner (web) uchun JSON tabiiy format;
///     <c>docs/research/03-asc-features-ux.md</c> §3.5 ham aynan "JSON design descriptor" ni tavsiya qiladi.</item>
/// </list>
/// <para>
/// Deserializatsiya QO'LDA yozilgan: <c>JsonSerializer</c> ning avtomatik xatolari
/// ("The JSON value could not be converted to...") foydalanuvchiga hech narsa aytmaydi.
/// Bu yerda har bir xato <see cref="PrintDesignException"/> bo'lib, yo'lni va
/// ruxsat etilgan qiymatlar ro'yxatini ko'rsatadi.
/// </para>
/// </remarks>
public static class PrintDesignLoader
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>JSON matnidan dizayn o'qiydi.</summary>
    /// <param name="json">Dizayn ta'rifi.</param>
    /// <param name="key">Dizayn kaliti (fayl nomi). Bo'sh bo'lsa JSON dagi <c>key</c> olinadi.</param>
    /// <exception cref="PrintDesignException">Ta'rif noto'g'ri.</exception>
    public static PrintDesign Load(string json, string? key = null)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new PrintDesignException(string.Empty, "ta'rif bo'sh.");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, DocumentOptions);
        }
        catch (JsonException ex)
        {
            throw new PrintDesignException(
                string.Empty,
                $"JSON sintaksisi buzilgan ({ex.LineNumber + 1}-qator, {ex.BytePositionInLine + 1}-belgi).",
                ex);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new PrintDesignException(
                    string.Empty,
                    $"ildiz obyekt (<c>{{ }}</c>) bo'lishi kerak, hozir — {Describe(root.ValueKind)}.");
            }

            var name = OptionalString(root, "name", "name") ?? key ?? "Nomsiz dizayn";
            var designKey = key ?? OptionalString(root, "key", "key") ?? name;

            var design = new PrintDesign
            {
                Key = designKey,
                Name = name,
                Description = OptionalString(root, "description", "description"),
                Scope = ReadEnum(root, "scope", "scope", ScopeNames, PrintScope.Class),
                Page = ReadPage(root),
                Theme = ReadTheme(root),
                Elements = ReadElements(root),
            };

            if (design.Elements.Count == 0)
                throw new PrintDesignException("elements", "kamida bitta element bo'lishi kerak.");

            if (design.Elements.OfType<PrintTimetableElement>().Count() > 1)
            {
                throw new PrintDesignException(
                    "elements",
                    "bitta dizaynda faqat BITTA \"timetable\" elementi bo'lishi mumkin.");
            }

            return design;
        }
    }

    /// <summary>Fayldan o'qiydi (kalit — fayl nomi kengaytmasiz).</summary>
    /// <param name="path">JSON fayl yo'li.</param>
    /// <exception cref="PrintDesignException">Fayl yo'q yoki ta'rif noto'g'ri.</exception>
    public static PrintDesign LoadFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new PrintDesignException(string.Empty, "fayl yo'li ko'rsatilmagan.");

        if (!File.Exists(path))
            throw new PrintDesignException(string.Empty, $"dizayn fayli topilmadi: {path}");

        return Load(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path));
    }

    // ------------------------------------------------------------------
    // Bo'limlar
    // ------------------------------------------------------------------

    private static PrintPage ReadPage(JsonElement root)
    {
        if (!root.TryGetProperty("page", out var page))
            return new PrintPage();

        RequireObject(page, "page");

        var margin = OptionalDouble(page, "marginMm", "page.marginMm") ?? 12;
        if (margin < 0 || margin > 60)
            throw new PrintDesignException("page.marginMm", $"chekka 0..60 mm oralig'ida bo'lishi kerak, berilgan: {Fmt(margin)}.");

        return new PrintPage(
            ReadEnum(page, "size", "page.size", PageSizeNames, PrintPageSize.A4),
            ReadEnum(page, "orientation", "page.orientation", OrientationNames, PrintOrientation.Landscape),
            margin);
    }

    private static PrintTheme ReadTheme(JsonElement root)
    {
        if (!root.TryGetProperty("theme", out var theme))
            return new PrintTheme();

        RequireObject(theme, "theme");
        var fallback = new PrintTheme();

        return new PrintTheme
        {
            Accent = ReadColor(theme, "accent", "theme.accent") ?? fallback.Accent,
            HeaderBackground = ReadColor(theme, "headerBackground", "theme.headerBackground") ?? fallback.HeaderBackground,
            HeaderForeground = ReadColor(theme, "headerForeground", "theme.headerForeground") ?? fallback.HeaderForeground,
            CardBackground = ReadColor(theme, "cardBackground", "theme.cardBackground") ?? fallback.CardBackground,
            CardForeground = ReadColor(theme, "cardForeground", "theme.cardForeground") ?? fallback.CardForeground,
            EmptyBackground = ReadColor(theme, "emptyBackground", "theme.emptyBackground") ?? fallback.EmptyBackground,
            GridLine = ReadColor(theme, "gridLine", "theme.gridLine") ?? fallback.GridLine,
            Muted = ReadColor(theme, "muted", "theme.muted") ?? fallback.Muted,
        };
    }

    private static IReadOnlyList<PrintElement> ReadElements(JsonElement root)
    {
        if (!root.TryGetProperty("elements", out var elements))
            throw new PrintDesignException("elements", "majburiy maydon yo'q.");

        if (elements.ValueKind != JsonValueKind.Array)
            throw new PrintDesignException("elements", $"ro'yxat ([ ]) bo'lishi kerak, hozir — {Describe(elements.ValueKind)}.");

        var result = new List<PrintElement>();
        var index = 0;

        foreach (var item in elements.EnumerateArray())
        {
            var path = $"elements[{index.ToString(CultureInfo.InvariantCulture)}]";
            index++;

            RequireObject(item, path);

            var type = OptionalString(item, "type", $"{path}.type")
                ?? throw new PrintDesignException($"{path}.type", "element turi ko'rsatilmagan. Ruxsat etilgan: " + string.Join(", ", ElementTypes));

            var rect = ReadRect(item, path);

            result.Add(type.Trim().ToLowerInvariant() switch
            {
                "text" => ReadText(item, path, rect),
                "line" or "box" => ReadLine(item, path, rect, isBox: type.Trim().Equals("box", StringComparison.OrdinalIgnoreCase)),
                "timetable" or "grid" => ReadGrid(item, path, rect),
                "legend" => ReadLegend(item, path, rect),
                _ => throw new PrintDesignException(
                    $"{path}.type",
                    $"noma'lum element turi \"{type}\". Ruxsat etilgan: {string.Join(", ", ElementTypes)}."),
            });
        }

        return result;
    }

    private static PrintTextElement ReadText(JsonElement item, string path, PrintRect rect) => new()
    {
        Rect = rect,
        Text = OptionalString(item, "text", $"{path}.text") ?? string.Empty,
        FontRatio = ReadFontRatio(item, "fontRatio", $"{path}.fontRatio", 0.02),
        Bold = OptionalBool(item, "bold", $"{path}.bold") ?? false,
        Italic = OptionalBool(item, "italic", $"{path}.italic") ?? false,
        Align = ReadEnum(item, "align", $"{path}.align", AlignNames, PrintAlign.Left),
        Color = ReadColor(item, "color", $"{path}.color"),
        Background = ReadColor(item, "background", $"{path}.background"),
    };

    private static PrintLineElement ReadLine(JsonElement item, string path, PrintRect rect, bool isBox)
    {
        var thickness = OptionalDouble(item, "thickness", $"{path}.thickness") ?? 1;
        if (thickness < 0 || thickness > 20)
            throw new PrintDesignException($"{path}.thickness", $"qalinlik 0..20 pt oralig'ida bo'lishi kerak, berilgan: {Fmt(thickness)}.");

        return new PrintLineElement
        {
            Rect = rect,
            Thickness = thickness,
            Color = ReadColor(item, "color", $"{path}.color"),
            Box = isBox || (OptionalBool(item, "box", $"{path}.box") ?? false),
            Fill = ReadColor(item, "fill", $"{path}.fill"),
        };
    }

    private static PrintTimetableElement ReadGrid(JsonElement item, string path, PrintRect rect)
    {
        var perPage = OptionalInt(item, "sectionsPerPage", $"{path}.sectionsPerPage") ?? 1;
        if (perPage < 1 || perPage > 40)
        {
            throw new PrintDesignException(
                $"{path}.sectionsPerPage",
                $"bitta sahifadagi to'rlar soni 1..40 bo'lishi kerak, berilgan: {perPage.ToString(CultureInfo.InvariantCulture)}.");
        }

        return new PrintTimetableElement
        {
            Rect = rect,
            Axis = ReadEnum(item, "axis", $"{path}.axis", AxisNames, PrintGridAxis.DaysAsColumns),
            ShowTime = OptionalBool(item, "showTime", $"{path}.showTime") ?? true,
            ShowTeacher = OptionalBool(item, "showTeacher", $"{path}.showTeacher") ?? true,
            ShowRoom = OptionalBool(item, "showRoom", $"{path}.showRoom") ?? true,
            ShowClass = OptionalBool(item, "showClass", $"{path}.showClass") ?? false,
            ShowGroup = OptionalBool(item, "showGroup", $"{path}.showGroup") ?? true,
            ShowWeeks = OptionalBool(item, "showWeeks", $"{path}.showWeeks") ?? true,
            ShowShift = OptionalBool(item, "showShift", $"{path}.showShift") ?? true,
            UseShortSubject = OptionalBool(item, "useShortSubject", $"{path}.useShortSubject") ?? false,
            HeaderFontRatio = ReadFontRatio(item, "headerFontRatio", $"{path}.headerFontRatio", 0.016),
            CellFontRatio = ReadFontRatio(item, "cellFontRatio", $"{path}.cellFontRatio", 0.014),
            CaptionFontRatio = ReadFontRatio(item, "captionFontRatio", $"{path}.captionFontRatio", 0.020),
            ColorBy = ReadEnum(item, "colorBy", $"{path}.colorBy", ColorSourceNames, PrintColorSource.Card),
            SectionsPerPage = perPage,
            ShowSectionCaption = OptionalBool(item, "showSectionCaption", $"{path}.showSectionCaption") ?? false,
        };
    }

    private static PrintLegendElement ReadLegend(JsonElement item, string path, PrintRect rect)
    {
        var columns = OptionalInt(item, "columns", $"{path}.columns") ?? 3;
        if (columns < 1 || columns > 8)
            throw new PrintDesignException($"{path}.columns", $"ustunlar soni 1..8 bo'lishi kerak, berilgan: {columns.ToString(CultureInfo.InvariantCulture)}.");

        var maxItems = OptionalInt(item, "maxItems", $"{path}.maxItems") ?? 60;
        if (maxItems < 1)
            throw new PrintDesignException($"{path}.maxItems", "kamida 1 bo'lishi kerak.");

        return new PrintLegendElement
        {
            Rect = rect,
            Legend = ReadEnum(item, "legend", $"{path}.legend", LegendNames, PrintLegendKind.Subjects),
            Title = OptionalString(item, "title", $"{path}.title"),
            Columns = columns,
            FontRatio = ReadFontRatio(item, "fontRatio", $"{path}.fontRatio", 0.012),
            ShowColors = OptionalBool(item, "showColors", $"{path}.showColors") ?? true,
            MaxItems = maxItems,
        };
    }

    // ------------------------------------------------------------------
    // Elementar o'qish
    // ------------------------------------------------------------------

    private static PrintRect ReadRect(JsonElement item, string path)
    {
        var rectPath = $"{path}.rect";

        if (!item.TryGetProperty("rect", out var rect))
            throw new PrintDesignException(rectPath, "majburiy maydon yo'q. Format: [chap, yuqori, o'ng, past] — 0..1 oraliqda.");

        if (rect.ValueKind != JsonValueKind.Array)
            throw new PrintDesignException(rectPath, $"[chap, yuqori, o'ng, past] ro'yxati bo'lishi kerak, hozir — {Describe(rect.ValueKind)}.");

        var values = new List<double>(4);
        foreach (var value in rect.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number))
                throw new PrintDesignException(rectPath, "barcha 4 qiymat son bo'lishi kerak.");

            values.Add(number);
        }

        if (values.Count != 4)
            throw new PrintDesignException(rectPath, $"aynan 4 ta qiymat kerak ([chap, yuqori, o'ng, past]), berilgan: {values.Count.ToString(CultureInfo.InvariantCulture)}.");

        foreach (var value in values)
        {
            if (value < 0 || value > 1)
                throw new PrintDesignException(rectPath, $"koordinata 0..1 normallashtirilgan oraliqda bo'lishi kerak, berilgan: {Fmt(value)}.");
        }

        var result = new PrintRect(values[0], values[1], values[2], values[3]);

        if (result.Width < 0)
            throw new PrintDesignException(rectPath, $"o'ng chegara ({Fmt(result.Right)}) chap chegaradan ({Fmt(result.Left)}) kichik.");

        if (result.Height < 0)
            throw new PrintDesignException(rectPath, $"past chegara ({Fmt(result.Bottom)}) yuqori chegaradan ({Fmt(result.Top)}) kichik.");

        return result;
    }

    private static double ReadFontRatio(JsonElement item, string property, string path, double fallback)
    {
        var value = OptionalDouble(item, property, path);
        if (value is null)
            return fallback;

        if (value <= 0 || value > 0.5)
        {
            throw new PrintDesignException(
                path,
                $"shrift nisbati 0 dan katta va 0.5 dan kichik bo'lishi kerak (sahifa balandligiga nisbatan), berilgan: {Fmt(value.Value)}.");
        }

        return value.Value;
    }

    private static string? ReadColor(JsonElement item, string property, string path)
    {
        var value = OptionalString(item, property, path);
        if (value is null)
            return null;

        var text = value.Trim();
        if (!PrintColor.IsValid(text))
        {
            throw new PrintDesignException(
                path,
                $"rang \"#RRGGBB\" ko'rinishida bo'lishi kerak (masalan \"#1E5AA8\"), berilgan: \"{value}\".");
        }

        return text.ToUpperInvariant();
    }

    private static TEnum ReadEnum<TEnum>(
        JsonElement item,
        string property,
        string path,
        IReadOnlyDictionary<string, TEnum> names,
        TEnum fallback)
        where TEnum : struct, Enum
    {
        var value = OptionalString(item, property, path);
        if (value is null)
            return fallback;

        if (names.TryGetValue(value.Trim().ToLowerInvariant(), out var parsed))
            return parsed;

        throw new PrintDesignException(
            path,
            $"noma'lum qiymat \"{value}\". Ruxsat etilgan: {string.Join(", ", names.Keys)}.");
    }

    private static string? OptionalString(JsonElement item, string property, string path)
    {
        if (!item.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;

        if (value.ValueKind != JsonValueKind.String)
            throw new PrintDesignException(path, $"matn bo'lishi kerak, hozir — {Describe(value.ValueKind)}.");

        return value.GetString();
    }

    private static bool? OptionalBool(JsonElement item, string property, string path)
    {
        if (!item.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new PrintDesignException(path, $"true yoki false bo'lishi kerak, hozir — {Describe(value.ValueKind)}."),
        };
    }

    private static double? OptionalDouble(JsonElement item, string property, string path)
    {
        if (!item.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number))
            throw new PrintDesignException(path, $"son bo'lishi kerak, hozir — {Describe(value.ValueKind)}.");

        return number;
    }

    private static int? OptionalInt(JsonElement item, string property, string path)
    {
        if (!item.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number))
            throw new PrintDesignException(path, $"butun son bo'lishi kerak, hozir — {Describe(value.ValueKind)}.");

        return number;
    }

    private static void RequireObject(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new PrintDesignException(path, $"obyekt ({{ }}) bo'lishi kerak, hozir — {Describe(element.ValueKind)}.");
    }

    private static string Describe(JsonValueKind kind) => kind switch
    {
        JsonValueKind.Array => "ro'yxat",
        JsonValueKind.Object => "obyekt",
        JsonValueKind.String => "matn",
        JsonValueKind.Number => "son",
        JsonValueKind.True or JsonValueKind.False => "mantiqiy qiymat",
        JsonValueKind.Null => "bo'sh (null)",
        _ => "noma'lum tur",
    };

    private static string Fmt(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    // ------------------------------------------------------------------
    // Nomlangan qiymatlar
    // ------------------------------------------------------------------

    private static readonly string[] ElementTypes = { "text", "line", "box", "timetable", "legend" };

    private static readonly Dictionary<string, PrintScope> ScopeNames = new(StringComparer.Ordinal)
    {
        ["class"] = PrintScope.Class,
        ["teacher"] = PrintScope.Teacher,
        ["room"] = PrintScope.Room,
        ["school"] = PrintScope.School,
    };

    private static readonly Dictionary<string, PrintPageSize> PageSizeNames = new(StringComparer.Ordinal)
    {
        ["a4"] = PrintPageSize.A4,
        ["a3"] = PrintPageSize.A3,
    };

    private static readonly Dictionary<string, PrintOrientation> OrientationNames = new(StringComparer.Ordinal)
    {
        ["portrait"] = PrintOrientation.Portrait,
        ["landscape"] = PrintOrientation.Landscape,
    };

    private static readonly Dictionary<string, PrintAlign> AlignNames = new(StringComparer.Ordinal)
    {
        ["left"] = PrintAlign.Left,
        ["center"] = PrintAlign.Center,
        ["right"] = PrintAlign.Right,
    };

    private static readonly Dictionary<string, PrintGridAxis> AxisNames = new(StringComparer.Ordinal)
    {
        ["days-as-columns"] = PrintGridAxis.DaysAsColumns,
        ["days-as-rows"] = PrintGridAxis.DaysAsRows,
    };

    private static readonly Dictionary<string, PrintLegendKind> LegendNames = new(StringComparer.Ordinal)
    {
        ["subjects"] = PrintLegendKind.Subjects,
        ["teachers"] = PrintLegendKind.Teachers,
        ["rooms"] = PrintLegendKind.Rooms,
        ["lessons"] = PrintLegendKind.Lessons,
    };

    private static readonly Dictionary<string, PrintColorSource> ColorSourceNames = new(StringComparer.Ordinal)
    {
        ["none"] = PrintColorSource.None,
        ["card"] = PrintColorSource.Card,
        ["subject"] = PrintColorSource.Subject,
    };
}
