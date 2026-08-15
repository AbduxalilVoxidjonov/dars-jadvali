using System.Globalization;
using System.Text;
using DarsJadvali.Domain.Common;

namespace DarsJadvali.Infrastructure.Export.Printing;

/// <summary>
/// Matn shablonidagi <c>{Token}</c> larni qiymatga almashtiradi.
/// </summary>
/// <remarks>
/// <para>
/// aSc <c>{#1035:#1635}</c> deb yozadi — bu "Class.Name" degani, lekin buni bilish uchun
/// <c>lang.asc</c> lug'atidan ID qidirish kerak. Dizaynni qo'lda o'qib bo'lmaydi.
/// Shuning uchun bu yerda tokenlar O'QILADIGAN nom bilan: <c>{Class.Name}</c>.
/// </para>
/// <para>
/// Noma'lum token JIMGINA yo'qolmaydi: u bo'sh matnga almashadi, ammo
/// <see cref="UnknownTokens"/> ro'yxatiga tushadi — shablon muallifi xatosini ko'radi.
/// </para>
/// </remarks>
public sealed class PrintTokenResolver
{
    private readonly Dictionary<string, string?> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly SortedSet<string> _unknown = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Qo'llab-quvvatlanadigan barcha token nomlari (hujjat va tekshiruv uchun).</summary>
    public static IReadOnlyList<string> KnownTokens { get; } = new[]
    {
        "School.Name", "AcademicYear", "Term",
        "Class.Name", "Teacher.Name", "Room.Name",
        "Scope.Name", "Scope.Title", "Scope.Kind",
        "Section.Caption", "Section.SubCaption",
        "Date", "Time", "Page", "PageCount", "App.Name",
    };

    /// <summary>Shablonda uchragan, lekin ma'lum bo'lmagan tokenlar.</summary>
    public IReadOnlyCollection<string> UnknownTokens => _unknown;

    /// <summary>Jadval ma'lumotlaridan token to'plamini quradi.</summary>
    /// <param name="timetable">Chop etiladigan jadval.</param>
    public PrintTokenResolver(PrintableTimetable timetable)
    {
        ArgumentNullException.ThrowIfNull(timetable);

        _values["School.Name"] = timetable.SchoolName;
        _values["AcademicYear"] = timetable.AcademicYear;
        _values["Term"] = timetable.Term;

        _values["Class.Name"] = timetable.Scope == PrintScope.Class ? timetable.ScopeName : null;
        _values["Teacher.Name"] = timetable.Scope == PrintScope.Teacher ? timetable.ScopeName : null;
        _values["Room.Name"] = timetable.Scope == PrintScope.Room ? timetable.ScopeName : null;

        _values["Scope.Name"] = timetable.ScopeName;
        _values["Scope.Title"] = timetable.ScopeTitle;
        _values["Scope.Kind"] = timetable.Scope switch
        {
            PrintScope.Class => "Sinf",
            PrintScope.Teacher => "O'qituvchi",
            PrintScope.Room => "Xona",
            _ => "Maktab",
        };

        _values["Date"] = timetable.FormattedDate;
        _values["Time"] = timetable.GeneratedAt.ToString("HH:mm", CultureInfo.InvariantCulture);
        _values["App.Name"] = AppInfo.AppName;

        _values["Page"] = "1";
        _values["PageCount"] = "1";
        _values["Section.Caption"] = timetable.Sections.Count > 0 ? timetable.Sections[0].Caption : null;
        _values["Section.SubCaption"] = timetable.Sections.Count > 0 ? timetable.Sections[0].SubCaption : null;
    }

    /// <summary>Sahifaga oid tokenlarni yangilaydi (har sahifa chizilishidan oldin).</summary>
    /// <param name="pageNumber">Joriy sahifa (1 dan).</param>
    /// <param name="pageCount">Jami sahifalar.</param>
    /// <param name="section">Shu sahifadagi birinchi to'r (bo'lmasa <c>null</c>).</param>
    public void SetPageContext(int pageNumber, int pageCount, PrintableSection? section)
    {
        _values["Page"] = pageNumber.ToString(CultureInfo.InvariantCulture);
        _values["PageCount"] = pageCount.ToString(CultureInfo.InvariantCulture);

        if (section is not null)
        {
            _values["Section.Caption"] = section.Caption;
            _values["Section.SubCaption"] = section.SubCaption;
        }
    }

    /// <summary>Qo'shimcha token qo'shadi yoki mavjudini almashtiradi.</summary>
    /// <param name="name">Token nomi (jingalak qavssiz).</param>
    /// <param name="value">Qiymat.</param>
    public void Set(string name, string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _values[name.Trim()] = value;
    }

    /// <summary>
    /// Shablondagi tokenlarni almashtiradi. <c>{{</c> — qochirilgan qavs (bitta <c>{</c> chiqadi).
    /// </summary>
    /// <param name="template">Matn shabloni.</param>
    public string Resolve(string? template)
    {
        if (string.IsNullOrEmpty(template))
            return string.Empty;

        var result = new StringBuilder(template!.Length + 16);
        var i = 0;

        while (i < template.Length)
        {
            var ch = template[i];

            if (ch == '{' && i + 1 < template.Length && template[i + 1] == '{')
            {
                result.Append('{');
                i += 2;
                continue;
            }

            if (ch == '}' && i + 1 < template.Length && template[i + 1] == '}')
            {
                result.Append('}');
                i += 2;
                continue;
            }

            if (ch != '{')
            {
                result.Append(ch);
                i++;
                continue;
            }

            var close = template.IndexOf('}', i + 1);
            if (close < 0)
            {
                // Yopilmagan qavs — matn sifatida qoladi (shablon buzilmasin).
                result.Append(template.AsSpan(i));
                break;
            }

            var name = template[(i + 1)..close].Trim();
            i = close + 1;

            if (name.Length == 0)
                continue;

            if (_values.TryGetValue(name, out var value))
            {
                result.Append(value ?? string.Empty);
                continue;
            }

            _unknown.Add(name);
        }

        // Ketma-ket bo'shliqlar va chekka tinish belgilari tozalanadi:
        // "{Class.Name} — {Term}" da Term bo'sh bo'lsa " — " osilib qolmasin.
        return Tidy(result.ToString());
    }

    /// <summary>Bo'sh tokenlardan qolgan ortiqcha ajratgichlarni tozalaydi.</summary>
    private static string Tidy(string text)
    {
        var collapsed = new StringBuilder(text.Length);
        var previousWasSpace = false;

        foreach (var ch in text)
        {
            var isSpace = ch == ' ' || ch == '\t';
            if (isSpace && previousWasSpace)
                continue;

            collapsed.Append(isSpace ? ' ' : ch);
            previousWasSpace = isSpace;
        }

        return collapsed.ToString().Trim().Trim('—', '-', '·', ',', ':').Trim();
    }
}
