namespace DarsJadvali.Infrastructure.Export.Printing;

/// <summary>Sahifa o'lchami.</summary>
public enum PrintPageSize
{
    /// <summary>210x297 mm.</summary>
    A4 = 0,

    /// <summary>297x420 mm — ko'p sinfli umumiy jadval uchun.</summary>
    A3 = 1,
}

/// <summary>Sahifa yo'nalishi.</summary>
public enum PrintOrientation
{
    /// <summary>Bo'yiga.</summary>
    Portrait = 0,

    /// <summary>Eniga.</summary>
    Landscape = 1,
}

/// <summary>Matnni gorizontal tekislash.</summary>
public enum PrintAlign
{
    /// <summary>Chapga.</summary>
    Left = 0,

    /// <summary>Markazga.</summary>
    Center = 1,

    /// <summary>O'ngga.</summary>
    Right = 2,
}

/// <summary>To'rning yo'nalishi.</summary>
public enum PrintGridAxis
{
    /// <summary>Kunlar — ustunlarda, soatlar — qatorlarda (klassik sinf jadvali).</summary>
    DaysAsColumns = 0,

    /// <summary>Kunlar — qatorlarda, soatlar — ustunlarda (aSc "internal_table" uslubi).</summary>
    DaysAsRows = 1,
}

/// <summary>Legenda turi — aSc <c>m_LegendaType</c> ning o'qiladigan ekvivalenti.</summary>
public enum PrintLegendKind
{
    /// <summary>Fanlar ro'yxati (aSc: 0).</summary>
    Subjects = 0,

    /// <summary>O'qituvchilar ro'yxati (aSc: 3).</summary>
    Teachers = 1,

    /// <summary>Xonalar ro'yxati (aSc: 2).</summary>
    Rooms = 2,

    /// <summary>Darslar jadvali: fan / o'qituvchi / soat (aSc: 8).</summary>
    Lessons = 3,
}

/// <summary>Katak fonini qaysi obyekt rangi bo'yicha bo'yash.</summary>
public enum PrintColorSource
{
    /// <summary>Rangsiz — dizayn foni.</summary>
    None = 0,

    /// <summary>Kartaning o'z rangi (<see cref="PrintableCard.ColorCode"/>).</summary>
    Card = 1,

    /// <summary>Fan nomidan barqaror hosil qilingan rang.</summary>
    Subject = 2,
}

/// <summary>
/// Sahifaga nisbatan NORMALLASHTIRILGAN to'rtburchak: 0.0 — chap/yuqori chekka, 1.0 — o'ng/past chekka.
/// </summary>
/// <remarks>
/// aSc 0..1 000 000 butun sonlarni ishlatadi; bu yerda 0..1 kasr son — o'qishga ham,
/// qo'lda yozishga ham qulay va bir xil "qog'ozdan mustaqil" xossani beradi.
/// </remarks>
/// <param name="Left">Chap chegara.</param>
/// <param name="Top">Yuqori chegara.</param>
/// <param name="Right">O'ng chegara.</param>
/// <param name="Bottom">Past chegara.</param>
public sealed record PrintRect(double Left, double Top, double Right, double Bottom)
{
    /// <summary>Kengligi (normallashtirilgan).</summary>
    public double Width => Right - Left;

    /// <summary>Balandligi (normallashtirilgan).</summary>
    public double Height => Bottom - Top;

    /// <summary>Butun sahifa.</summary>
    public static PrintRect Full { get; } = new(0, 0, 1, 1);
}

/// <summary>Sahifa sozlamasi.</summary>
/// <param name="Size">Qog'oz o'lchami.</param>
/// <param name="Orientation">Yo'nalish.</param>
/// <param name="MarginMm">Chekka (mm) — normallashtirilgan koordinatalar shu chekkadan ichkarida hisoblanadi.</param>
public sealed record PrintPage(
    PrintPageSize Size = PrintPageSize.A4,
    PrintOrientation Orientation = PrintOrientation.Landscape,
    double MarginMm = 12);

/// <summary>Dizaynning umumiy rang sxemasi — barcha elementlar shundan standart qiymat oladi.</summary>
public sealed record PrintTheme
{
    /// <summary>Asosiy (brend) rang: sarlavha, chegara.</summary>
    public string Accent { get; init; } = "#1E5AA8";

    /// <summary>To'r sarlavhasi foni.</summary>
    public string HeaderBackground { get; init; } = "#1E5AA8";

    /// <summary>To'r sarlavhasi matni.</summary>
    public string HeaderForeground { get; init; } = "#FFFFFF";

    /// <summary>Dars katagi foni.</summary>
    public string CardBackground { get; init; } = "#EAF1FB";

    /// <summary>Dars katagi matni.</summary>
    public string CardForeground { get; init; } = "#102A43";

    /// <summary>Bo'sh katak foni.</summary>
    public string EmptyBackground { get; init; } = "#FFFFFF";

    /// <summary>To'r chiziqlari.</summary>
    public string GridLine { get; init; } = "#7A93B8";

    /// <summary>Ikkilamchi matn (o'qituvchi, xona).</summary>
    public string Muted { get; init; } = "#54687F";
}

/// <summary>Dizayn elementining umumiy qismi.</summary>
public abstract record PrintElement
{
    /// <summary>Joylashuvi (normallashtirilgan).</summary>
    public PrintRect Rect { get; init; } = PrintRect.Full;
}

/// <summary>
/// Matn yoki sarlavha. <see cref="Text"/> ichida bog'lash tokenlari bo'lishi mumkin
/// (<c>{Class.Name}</c>, <c>{School.Name}</c> ...).
/// </summary>
public sealed record PrintTextElement : PrintElement
{
    /// <summary>Matn shabloni.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Shrift o'lchami sahifa balandligiga nisbatan (aSc <c>font/@ratio</c>).</summary>
    public double FontRatio { get; init; } = 0.02;

    /// <summary>Qalin.</summary>
    public bool Bold { get; init; }

    /// <summary>Kursiv.</summary>
    public bool Italic { get; init; }

    /// <summary>Tekislash.</summary>
    public PrintAlign Align { get; init; } = PrintAlign.Left;

    /// <summary>Matn rangi. <c>null</c> — qora.</summary>
    public string? Color { get; init; }

    /// <summary>Fon rangi. <c>null</c> — shaffof.</summary>
    public string? Background { get; init; }
}

/// <summary>Chiziq yoki ramka.</summary>
public sealed record PrintLineElement : PrintElement
{
    /// <summary>Qalinlik punktda.</summary>
    public double Thickness { get; init; } = 1;

    /// <summary>Rang. <c>null</c> — mavzu <see cref="PrintTheme.Accent"/> rangi.</summary>
    public string? Color { get; init; }

    /// <summary>Ha bo'lsa — to'rtburchak ramka; yo'q bo'lsa — chiziq.</summary>
    public bool Box { get; init; }

    /// <summary>Ramka ichini bo'yash rangi (faqat <see cref="Box"/> bilan).</summary>
    public string? Fill { get; init; }
}

/// <summary>
/// Jadval to'ri. aSc dan farqli o'laroq bu yerda to'rning O'ZI ham ta'riflanadi
/// (o'q yo'nalishi, katakda nima ko'rinishi) — aSc'da bu ma'lumot dizayn faylida
/// umuman yo'q va chop etish oynasidan olinadi, natijada dizayn "yarim" bo'ladi.
/// </summary>
public sealed record PrintTimetableElement : PrintElement
{
    /// <summary>Kunlar ustunlarda yoki qatorlarda.</summary>
    public PrintGridAxis Axis { get; init; } = PrintGridAxis.DaysAsColumns;

    /// <summary>Soat ustunida vaqt oralig'i ko'rsatilsinmi.</summary>
    public bool ShowTime { get; init; } = true;

    /// <summary>Katakda o'qituvchi.</summary>
    public bool ShowTeacher { get; init; } = true;

    /// <summary>Katakda xona.</summary>
    public bool ShowRoom { get; init; } = true;

    /// <summary>Katakda sinf (o'qituvchi jadvali uchun muhim).</summary>
    public bool ShowClass { get; init; }

    /// <summary>Katakda guruh nomi.</summary>
    public bool ShowGroup { get; init; } = true;

    /// <summary>A/B hafta belgisi.</summary>
    public bool ShowWeeks { get; init; } = true;

    /// <summary>Smena polosasi (1-smena / 2-smena) ko'rsatilsinmi.</summary>
    public bool ShowShift { get; init; } = true;

    /// <summary>Fan nomi o'rniga qisqartma ishlatilsinmi.</summary>
    public bool UseShortSubject { get; init; }

    /// <summary>Sarlavha shrifti nisbati.</summary>
    public double HeaderFontRatio { get; init; } = 0.016;

    /// <summary>Katak shrifti nisbati.</summary>
    public double CellFontRatio { get; init; } = 0.014;

    /// <summary>To'r sarlavhasi (sinf nomi) shrifti nisbati.</summary>
    public double CaptionFontRatio { get; init; } = 0.020;

    /// <summary>Katak rangi manbai.</summary>
    public PrintColorSource ColorBy { get; init; } = PrintColorSource.Card;

    /// <summary>Bitta sahifaga nechta to'r (sinf) sig'adi. Ko'p sahifali bo'linish shu bo'yicha.</summary>
    public int SectionsPerPage { get; init; } = 1;

    /// <summary>To'r sarlavhasi (sinf nomi) ko'rsatilsinmi. Bitta to'rli dizaynda odatda yo'q.</summary>
    public bool ShowSectionCaption { get; init; }
}

/// <summary>Legenda: fanlar / o'qituvchilar / xonalar ro'yxati yoki darslar jadvali.</summary>
public sealed record PrintLegendElement : PrintElement
{
    /// <summary>Legenda turi.</summary>
    public PrintLegendKind Legend { get; init; } = PrintLegendKind.Subjects;

    /// <summary>Legenda sarlavhasi. <c>null</c> — turiga mos standart nom.</summary>
    public string? Title { get; init; }

    /// <summary>Necha ustunga yoyilsin (aSc <c>m_nColumns</c>).</summary>
    public int Columns { get; init; } = 3;

    /// <summary>Shrift nisbati.</summary>
    public double FontRatio { get; init; } = 0.012;

    /// <summary>Rang namunasi (kvadratcha) ko'rsatilsinmi.</summary>
    public bool ShowColors { get; init; } = true;

    /// <summary>Ko'rsatiladigan eng ko'p yozuv soni (sig'masa kesiladi).</summary>
    public int MaxItems { get; init; } = 60;
}

/// <summary>
/// Chop etish dizayni — aSc <c>designs/&lt;Name&gt;/def.xml</c> ning o'qiladigan ekvivalenti.
/// </summary>
public sealed record PrintDesign
{
    /// <summary>Fayl/kalit nomi ("sinf-kok").</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>Foydalanuvchiga ko'rinadigan nom.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Qisqacha tavsif.</summary>
    public string? Description { get; init; }

    /// <summary>Mo'ljallangan qamrov.</summary>
    public PrintScope Scope { get; init; } = PrintScope.Class;

    /// <summary>Sahifa sozlamasi.</summary>
    public PrintPage Page { get; init; } = new();

    /// <summary>Rang sxemasi.</summary>
    public PrintTheme Theme { get; init; } = new();

    /// <summary>Elementlar — ro'yxat tartibi = chizish tartibi (z-tartib).</summary>
    public IReadOnlyList<PrintElement> Elements { get; init; } = Array.Empty<PrintElement>();

    /// <summary>Dizayndagi (yagona) to'r elementi. Bo'lmasa <c>null</c>.</summary>
    public PrintTimetableElement? Grid => Elements.OfType<PrintTimetableElement>().FirstOrDefault();
}
