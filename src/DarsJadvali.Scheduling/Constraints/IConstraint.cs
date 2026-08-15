using DarsJadvali.Scheduling.Model;

namespace DarsJadvali.Scheduling.Constraints;

/// <summary>
/// aSc `Importance` pog'onalari (#3064 Normal, #3065 Low, #3066 High, #3073 Strict).
/// Og'irliklar 0..1000 shkalada (02-asc-.., 1-bo'lim).
/// </summary>
public enum Importance
{
    /// <summary>#3065 — w = 10.</summary>
    Low = 10,

    /// <summary>#3064 — w = 100.</summary>
    Normal = 100,

    /// <summary>#3066 — w = 500.</summary>
    High = 500,

    /// <summary>#3073 — hard, buzilmaydi.</summary>
    Strict = 1_000_000,
}

/// <summary>Jarima hisoblanadigan mustaqil bo'lak (constraint dekompozitsiyasi).</summary>
public enum ScopeKind
{
    Global = 0,

    /// <summary>A = teacherId, B = dayIndex.</summary>
    TeacherDay = 1,

    /// <summary>A = teacherId, B = weekIndex.</summary>
    TeacherWeek = 2,

    /// <summary>A = classId, B = dayIndex.</summary>
    ClassDay = 3,

    /// <summary>A = classId, B = weekIndex.</summary>
    ClassWeek = 4,

    /// <summary>A = classId, B = subjectId.</summary>
    ClassSubject = 5,

    /// <summary>A = cardId.</summary>
    Card = 6,

    /// <summary>A = groupId, B = dayIndex.</summary>
    GroupDay = 7,
}

/// <summary>Jarima hisoblanadigan bo'lak identifikatori.</summary>
public readonly record struct Scope(ScopeKind Kind, int A, int B);

/// <summary>
/// Cheklov abstraksiyasi (02-asc-.., 4.1).
/// <b>Asosiy invariant:</b> <c>Evaluate(s) == Sum(EnumerateScopes -> Weight * ScopeViolation)</c>,
/// va <c>DeltaPenalty(m) == Evaluate(after) - Evaluate(before)</c>, chunki
/// <see cref="AffectedScopes"/> qiymati o'zgarishi mumkin bo'lgan barcha scope'larni qamrab oladi.
/// </summary>
public interface IConstraint
{
    /// <summary>Katalog ID'si, masalan "C-TCH-01".</summary>
    string Id { get; }

    /// <summary>O'qiladigan nomi.</summary>
    string Name { get; }

    /// <summary>aSc Importance (#3063).</summary>
    Importance Importance { get; set; }

    /// <summary>Jarima og'irligi (w).</summary>
    int Weight { get; set; }

    /// <summary>Yoqilganmi (Faza 5 — relaxation shu bayroqni o'zgartiradi).</summary>
    bool Enabled { get; set; }

    /// <summary>#3072 "Allow relaxation".</summary>
    bool AllowRelaxation { get; set; }

    /// <summary>Hard cheklovmi (Importance == Strict).</summary>
    bool IsHard { get; }

    /// <summary>Barcha scope'lar (to'liq baholash uchun).</summary>
    void EnumerateScopes(SolutionState state, List<Scope> output);

    /// <summary>Harakat ta'sir qilishi mumkin bo'lgan scope'lar (superset bo'lishi mumkin).</summary>
    void AffectedScopes(SolutionState state, Move move, List<Scope> output);

    /// <summary>Bitta scope bo'yicha buzilish kattaligi (v_k).</summary>
    long ScopeViolation(SolutionState state, in Scope scope);

    /// <summary>To'liq jarima = Sum(Weight * v_k).</summary>
    long Evaluate(SolutionState state);
}
