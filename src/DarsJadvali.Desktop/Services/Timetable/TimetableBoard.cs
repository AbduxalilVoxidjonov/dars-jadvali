using DarsJadvali.Desktop.Models;
using DarsJadvali.Domain.Enums;

namespace DarsJadvali.Desktop.Services.Timetable;

/// <summary>
/// Jadvalning xotiradagi holati: kartalar, bandlik indeksi va <b>tez baholash</b>.
/// </summary>
/// <remarks>
/// <para>
/// Bu sinf Avalonia'ga bog'liq emas — to'liq sinovdan o'tkaziladi. Bazaga murojaat qilmaydi:
/// barcha tekshiruv <see cref="TimetableRuleSet"/> keshi va lug'at indekslari ustida bajariladi,
/// shuning uchun bitta baholash O(kartadagi soatlar × slotdagi kartalar) — amalda mikrosoniyalar.
/// </para>
/// <para>
/// <b>Qoidalarning yagona manbasi.</b> Kun faolligi, kunlik dars chegarasi, o'qituvchining
/// ish vaqti va haftalik me'yor bu yerda <b>hisoblanmaydi</b> — ular
/// <c>Application.Validation.ScheduleSnapshot</c> dan <see cref="TimetableRuleSet.FromSnapshot"/>
/// orqali ko'chiriladi. Board faqat <b>xotiradagi (hali saqlanmagan)</b> holatga tegishli
/// bandlik tekshiruvini o'zi bajaradi — uni Application ko'ra olmaydi, chunki u bazadagi
/// holatni biladi. Ikkalasining bir xil natija berishi
/// <c>TimetableBoardRuleParityTests</c> bilan isbotlangan.
/// </para>
/// <para>
/// O'zgarish tarixi bu yerda emas — <see cref="ICommandHistory"/> da. Board faqat holatni saqlaydi.
/// </para>
/// </remarks>
public sealed class TimetableBoard
{
    private readonly List<TimetableCard> _cards = new();
    private readonly Dictionary<(WeekDay Day, int Period), List<TimetableCard>> _bySlot = new();
    private readonly HashSet<int> _dirty = new();
    private readonly HashSet<int> _lockDirty = new();
    private readonly HashSet<int> _moveDirty = new();

    /// <summary>Bo'sh taxta yaratadi.</summary>
    public TimetableBoard()
        : this(TimetableRuleSet.Empty)
    {
    }

    /// <summary>Berilgan qoidalar bilan taxta yaratadi.</summary>
    /// <param name="rules">Baholash qoidalari keshi.</param>
    public TimetableBoard(TimetableRuleSet rules)
    {
        Rules = rules ?? throw new ArgumentNullException(nameof(rules));
    }

    /// <summary>Taxta o'zgarganda ko'tariladi (qayta chizish uchun).</summary>
    public event EventHandler? Changed;

    /// <summary>Baholash qoidalari.</summary>
    public TimetableRuleSet Rules { get; private set; }

    /// <summary>Barcha kartalar (qo'yilgan va qo'yilmagan).</summary>
    public IReadOnlyList<TimetableCard> Cards => _cards;

    /// <summary>Joylashtirilmagan kartalar (o'ng paneldagi ro'yxat).</summary>
    public IEnumerable<TimetableCard> UnplacedCards => _cards.Where(c => !c.IsPlaced);

    /// <summary>Bazaga yozilishi kutilayotgan kartalar identifikatorlari.</summary>
    public IReadOnlyCollection<int> DirtyCardIds => _dirty;

    /// <summary>Qulfi o'zgargan va hali bazaga yozilmagan kartalar.</summary>
    public IReadOnlyCollection<int> LockDirtyCardIds => _lockDirty;

    /// <summary>Kartaning qulfi o'zgarganmi (hali yozilmagan).</summary>
    public bool IsLockDirty(int cardId) => _lockDirty.Contains(cardId);

    /// <summary>Kartaning joyi o'zgarganmi (hali yozilmagan).</summary>
    public bool IsMoveDirty(int cardId) => _moveDirty.Contains(cardId);

    /// <summary>Taxtani yangi ma'lumot bilan to'ldiradi (avvalgi holat butunlay almashadi).</summary>
    public void Load(IEnumerable<TimetableCard> cards, TimetableRuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(rules);

        Rules = rules;
        _cards.Clear();
        _bySlot.Clear();
        _dirty.Clear();
        _lockDirty.Clear();
        _moveDirty.Clear();

        foreach (var card in cards)
        {
            _cards.Add(card);
            IndexCard(card);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Bitta karta qo'shadi (masalan qo'lda yaratilgan).</summary>
    public void AddCard(TimetableCard card)
    {
        ArgumentNullException.ThrowIfNull(card);

        _cards.Add(card);
        IndexCard(card);
        MarkMoved(card);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Kartani butunlay o'chiradi.</summary>
    public void RemoveCard(TimetableCard card)
    {
        ArgumentNullException.ThrowIfNull(card);

        UnindexCard(card);
        _cards.Remove(card);
        MarkMoved(card);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Identifikator bo'yicha kartani topadi.</summary>
    public TimetableCard? FindById(int id) => _cards.FirstOrDefault(c => c.Id == id);

    /// <summary>
    /// Kartani ko'chiradi (<paramref name="position"/> <c>null</c> bo'lsa — joylashtirilmaganlar
    /// paneliga qaytaradi). Tekshiruv qilmaydi: uni chaqiruvchi <see cref="Evaluate"/> bilan bajaradi.
    /// </summary>
    public void MoveCard(TimetableCard card, SlotPosition? position)
    {
        ArgumentNullException.ThrowIfNull(card);

        UnindexCard(card);
        card.MoveTo(position);
        IndexCard(card);
        MarkMoved(card);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Kartani qulflaydi/qulfdan chiqaradi.</summary>
    public void SetLock(TimetableCard card, bool locked)
    {
        ArgumentNullException.ThrowIfNull(card);

        card.IsLocked = locked;
        MarkDirty(card);
        _lockDirty.Add(card.Id);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Shu pozitsiyani egallab turgan kartalar (juft dars ham hisobga olinadi).</summary>
    public IReadOnlyList<TimetableCard> CardsAt(WeekDay day, int period)
        => _bySlot.TryGetValue((day, period), out var list)
            ? list
            : (IReadOnlyList<TimetableCard>)Array.Empty<TimetableCard>();

    /// <summary>Bazaga yozilgandan keyin "iflos" belgilarini tozalaydi.</summary>
    public void ClearDirty()
    {
        _dirty.Clear();
        _lockDirty.Clear();
        _moveDirty.Clear();
    }

    /// <summary>
    /// Kartani berilgan pozitsiyaga qo'yish mumkinmi — aSc'dagi uch darajali baho.
    /// </summary>
    /// <param name="card">Baholanayotgan karta.</param>
    /// <param name="day">Kun.</param>
    /// <param name="period">Boshlanish dars raqami (1-based).</param>
    /// <param name="ignore">Hisobga olinmaydigan kartalar (guruh ko'chishida birga ketayotganlar).</param>
    public PlacementEvaluation Evaluate(
        TimetableCard card,
        WeekDay day,
        int period,
        IReadOnlyCollection<TimetableCard>? ignore = null)
    {
        ArgumentNullException.ThrowIfNull(card);

        // 0. Qulflangan karta umuman ko'chmaydi (aSc §4.5).
        if (card.IsLocked)
        {
            return PlacementEvaluation.Forbid("Karta qulflangan — avval qulfni oching.");
        }

        // 1. Kun faol emas.
        if (!Rules.IsActiveDay(day))
        {
            return PlacementEvaluation.Forbid($"{day.ToUzbek()} kuni dars o'tilmaydi.");
        }

        // 2. Dars raqami chegaradan chiqib ketdi — juft dars butunlay sig'ishi kerak.
        var maxPeriod = Rules.MaxPeriodOf(day);
        if (period < 1 || period + card.Length - 1 > maxPeriod)
        {
            return PlacementEvaluation.Forbid(card.IsDouble
                ? $"Juft dars {day.ToUzbek()} kuniga sig'maydi (oxirgi soat — {maxPeriod})."
                : $"{day.ToUzbek()} kuni {period}-soat yo'q (1–{maxPeriod}).");
        }

        var warnings = new List<string>();

        // 3-6. Har bir egallanadigan soat bo'yicha to'qnashuv tekshiruvi.
        foreach (var slot in card.PeriodsFrom(period))
        {
            foreach (var other in CardsAt(day, slot))
            {
                if (ReferenceEquals(other, card) || (ignore is not null && ignore.Contains(other)))
                {
                    continue;
                }

                // Turli haftalarda o'tiladigan kartalar bir-biriga xalaqit bermaydi.
                if (!card.OverlapsWeeks(other))
                {
                    continue;
                }

                if (card.SharesClassResource(other))
                {
                    return PlacementEvaluation.Forbid(
                        $"{card.ScopeText}: {day.ToUzbek()} {slot}-soat band ({other.SubjectName}).");
                }

                if (card.TeacherIds.Any(t => other.TeacherIds.Contains(t)))
                {
                    return PlacementEvaluation.Forbid(
                        $"O'qituvchi band: {day.ToUzbek()} {slot}-soat, {other.ScopeText}.");
                }

                if (!string.IsNullOrWhiteSpace(card.RoomNumber) &&
                    string.Equals(card.RoomNumber, other.RoomNumber, StringComparison.OrdinalIgnoreCase))
                {
                    return PlacementEvaluation.Forbid(
                        $"{card.RoomNumber}-xona {day.ToUzbek()} {slot}-soatda band.");
                }
            }

            // 7. O'qituvchining ish vaqti (TEACHER_UNAVAILABLE).
            foreach (var teacherId in card.TeacherIds)
            {
                if (Rules.IsTeacherBlocked(teacherId, day, slot))
                {
                    return PlacementEvaluation.Forbid(
                        $"O'qituvchi {day.ToUzbek()} kuni {slot}-soatda ishlamaydi.");
                }
            }
        }

        // 8. Ogohlantirish: shu fan shu kuni allaqachon bor (SUBJECT_REPEATED_IN_DAY).
        var repeated = _cards.Any(c =>
            !ReferenceEquals(c, card) &&
            (ignore is null || !ignore.Contains(c)) &&
            c.Day == day &&
            c.SubjectId == card.SubjectId &&
            c.SharesClassResource(card));

        if (repeated)
        {
            warnings.Add($"{card.SubjectName} fani {day.ToUzbek()} kuni allaqachon o'tiladi.");
        }

        // 9. Ogohlantirish: haftalik me'yordan oshib ketish (WEEKLY_HOURS_EXCEEDED).
        var quota = card.TeacherIds.Count == 1
            ? Rules.WeeklyQuota(card.TeacherIds[0], card.SubjectId, card.ClassGroupId)
            : 0;

        if (quota > 0)
        {
            var placed = _cards.Count(c =>
                c.IsPlaced &&
                !ReferenceEquals(c, card) &&
                c.SubjectId == card.SubjectId &&
                c.ClassGroupId == card.ClassGroupId &&
                c.TeacherIds.Count == 1 &&
                c.TeacherIds[0] == card.TeacherIds[0]);

            if (placed + card.Length > quota)
            {
                warnings.Add($"Haftalik me'yor {quota} soat — bu karta bilan {placed + card.Length} bo'ladi.");
            }
        }

        // 10. Ogohlantirish: kun ichida "oyna" (bo'sh soat) paydo bo'lsa.
        if (CreatesGap(card, day, period, ignore))
        {
            warnings.Add("Sinf jadvalida oyna (bo'sh soat) paydo bo'ladi.");
        }

        return warnings.Count == 0 ? PlacementEvaluation.Preferred : PlacementEvaluation.Warn(warnings);
    }

    /// <summary>
    /// <c>SHIFT</c> bosilganda yoritiladigan barcha mumkin pozitsiyalar (aSc §4.2).
    /// </summary>
    public IReadOnlyList<SlotPosition> PossiblePositions(
        TimetableCard card,
        IReadOnlyCollection<TimetableCard>? ignore = null)
    {
        ArgumentNullException.ThrowIfNull(card);

        var result = new List<SlotPosition>();

        foreach (var day in Rules.Days)
        {
            var max = Rules.MaxPeriodOf(day);

            // Ikki smenada raqamlar uzluksiz (1-smena 1..6, 2-smena 7..12), lekin ro'yxat
            // qo'ng'iroq jadvalidan keladi — 1..max deb taxmin qilinmaydi.
            foreach (var period in Rules.PeriodNumbers)
            {
                if (period + card.Length - 1 > max)
                {
                    continue;
                }

                if (Evaluate(card, day, period, ignore).IsAllowed)
                {
                    result.Add(new SlotPosition(day, period));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Bo'sh katakda o'ng tugma — shu pozitsiyaga qo'yish mumkin bo'lgan kartalar
    /// (aSc §4.3 "teskari qidiruv").
    /// </summary>
    public IReadOnlyList<TimetableCard> CandidatesFor(WeekDay day, int period)
    {
        var result = new List<TimetableCard>();

        foreach (var card in _cards)
        {
            if (card.IsLocked || (card.Day == day && card.Period == period))
            {
                continue;
            }

            if (Evaluate(card, day, period).IsAllowed)
            {
                result.Add(card);
            }
        }

        return result;
    }

    /// <summary>Kartani shu pozitsiyaga qo'yish sinf kunida oyna hosil qiladimi.</summary>
    private bool CreatesGap(
        TimetableCard card,
        WeekDay day,
        int period,
        IReadOnlyCollection<TimetableCard>? ignore)
    {
        var used = new HashSet<int>(card.PeriodsFrom(period));

        foreach (var other in _cards)
        {
            if (ReferenceEquals(other, card) ||
                other.Day != day ||
                !other.IsPlaced ||
                (ignore is not null && ignore.Contains(other)) ||
                !other.SharesClassResource(card))
            {
                continue;
            }

            foreach (var slot in other.OccupiedPeriods)
            {
                used.Add(slot);
            }
        }

        if (used.Count < 2)
        {
            return false;
        }

        var min = used.Min();
        var max = used.Max();
        return max - min + 1 > used.Count;
    }

    private void MarkDirty(TimetableCard card) => _dirty.Add(card.Id);

    /// <summary>Kartaning joyi o'zgardi — bazaga ko'chirish so'rovi yuboriladi.</summary>
    private void MarkMoved(TimetableCard card)
    {
        _dirty.Add(card.Id);
        _moveDirty.Add(card.Id);
    }

    private void IndexCard(TimetableCard card)
    {
        if (!card.IsPlaced)
        {
            return;
        }

        var day = card.Day!.Value;
        foreach (var slot in card.OccupiedPeriods)
        {
            var key = (day, slot);
            if (!_bySlot.TryGetValue(key, out var list))
            {
                list = new List<TimetableCard>();
                _bySlot[key] = list;
            }

            list.Add(card);
        }
    }

    private void UnindexCard(TimetableCard card)
    {
        if (!card.IsPlaced)
        {
            return;
        }

        var day = card.Day!.Value;
        foreach (var slot in card.OccupiedPeriods)
        {
            if (_bySlot.TryGetValue((day, slot), out var list))
            {
                list.Remove(card);
            }
        }
    }
}
