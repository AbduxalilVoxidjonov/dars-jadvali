namespace DarsJadvali.Scheduling.Constraints;

/// <summary>Soft cheklovlar to'plami. Tartib determinizm uchun barqaror.</summary>
public sealed class ConstraintSet
{
    private readonly List<IConstraint> _items = new();

    public IReadOnlyList<IConstraint> Items => _items;

    public ConstraintSet Add(IConstraint c)
    {
        _items.Add(c);
        return this;
    }

    public IConstraint? Find(string id) => _items.FirstOrDefault(c => c.Id == id);

    /// <summary>
    /// v1 standart to'plami (02-asc-.., 2.3 og'irliklari bilan):
    /// C-CLS-01 (800), C-DST-05 (600), C-DST-01 (500), C-TCH-07/08 (400), C-TCH-10 (400),
    /// C-CLS-03 (400), C-TCH-01 (300), C-TCH-02 (300), C-TCH-14/15 (300), C-AVL-06 (100).
    /// </summary>
    public static ConstraintSet CreateDefault()
    {
        var s = new ConstraintSet();
        s.Add(new ClassGapsConstraint());                 // C-CLS-01, w=800
        s.Add(new SubjectOncePerDayConstraint());         // C-DST-05, w=600
        s.Add(new EquableDistributionConstraint());       // C-DST-01, w=500
        s.Add(new TeacherDaysTaughtConstraint());         // C-TCH-07/08, w=400
        s.Add(new TeacherMaxConsecutiveConstraint());     // C-TCH-10, w=400
        s.Add(new ClassDailyLoadConstraint());            // C-CLS-03, w=400
        s.Add(new TeacherGapsPerWeekConstraint());        // C-TCH-01, w=300
        s.Add(new TeacherGapsPerDayConstraint());         // C-TCH-02, w=300
        s.Add(new TeacherDailyLoadConstraint());          // C-TCH-14/15, w=300
        s.Add(new QuestionMarkedPositionConstraint());    // C-AVL-06, w=100
        return s;
    }
}
