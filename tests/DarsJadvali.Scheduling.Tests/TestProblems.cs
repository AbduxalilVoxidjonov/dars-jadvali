using DarsJadvali.Scheduling.Model;
using DarsJadvali.Scheduling.Util;

namespace DarsJadvali.Scheduling.Tests;

/// <summary>Testlar uchun standart masalalar.</summary>
public static class TestProblems
{
    /// <summary>Bitta o'qituvchi, ikki sinf — o'qituvchi ikki joyda bo'la olmasligi (T-H-01).</summary>
    public static Problem OneTeacherTwoClasses(int periodsEach = 3, int days = 5, int periods = 6)
    {
        var b = new ProblemBuilder(new TimeGrid(days, periods));
        var t = b.AddTeacher("Ali");
        var s = b.AddSubject("Matematika");
        for (int i = 0; i < 2; i++)
        {
            var c = b.AddClass($"5-{(char)('A' + i)}", 25);
            var g = b.AddEntireClassGroup(c);
            b.AddLesson(s, new[] { t }, new[] { g }, periodsEach);
        }
        return b.Build();
    }

    /// <summary>Bo'linishli sinf: 2 ta guruh bir divisiontag'da, yana 2 tasi boshqa tag'da.</summary>
    public static Problem DividedClass(out GroupDef whole, out GroupDef d1g1, out GroupDef d1g2,
                                       out GroupDef d2g1, out GroupDef d2g2)
    {
        var b = new ProblemBuilder(new TimeGrid(5, 6));
        var cls = b.AddClass("7-A", 30);
        whole = b.AddEntireClassGroup(cls);
        d1g1 = b.AddGroup(cls, "1-guruh", 1, 15);
        d1g2 = b.AddGroup(cls, "2-guruh", 1, 15);
        d2g1 = b.AddGroup(cls, "O'g'illar", 2, 16);
        d2g2 = b.AddGroup(cls, "Qizlar", 2, 14);

        var eng = b.AddSubject("Ingliz tili");
        var pe = b.AddSubject("Jismoniy tarbiya");
        var t1 = b.AddTeacher("T1");
        var t2 = b.AddTeacher("T2");
        var t3 = b.AddTeacher("T3");
        var t4 = b.AddTeacher("T4");

        b.AddLesson(eng, new[] { t1 }, new[] { d1g1 }, 2);
        b.AddLesson(eng, new[] { t2 }, new[] { d1g2 }, 2);
        b.AddLesson(pe, new[] { t3 }, new[] { d2g1 }, 2);
        b.AddLesson(pe, new[] { t4 }, new[] { d2g2 }, 2);
        return b.Build();
    }

    /// <summary>Kichik realistik maktab: 6 sinf x ~30 dars (T-I-01).</summary>
    public static Problem SmallSchool(int classCount = 6, int days = 5, int periods = 7)
    {
        var b = new ProblemBuilder(new TimeGrid(days, periods));
        string[] subjectNames = { "Matematika", "Ona tili", "Ingliz tili", "Tarix", "Biologiya", "Fizika", "Jismoniy tarbiya" };
        int[] hours = { 5, 5, 4, 3, 3, 3, 2 };

        var subjects = subjectNames.Select(b.AddSubject).ToArray();

        // Har fan uchun o'qituvchilar soni yukka qarab tanlanadi (aks holda masala imkonsiz bo'ladi).
        int slots = days * periods;
        var pool = new List<TeacherDef>[subjects.Length];
        for (int si = 0; si < subjects.Length; si++)
        {
            int need = Math.Max(2, (int)Math.Ceiling(classCount * hours[si] / (slots * 0.65)));
            pool[si] = new List<TeacherDef>();
            for (int i = 0; i < need; i++)
            {
                var t = b.AddTeacher($"{subjectNames[si]}-{i + 1}");
                t.MaxGapsPerDay = 2;
                t.MaxConsecutivePeriods = 4;
                pool[si].Add(t);
            }
        }

        for (int ci = 0; ci < classCount; ci++)
        {
            var cls = b.AddClass($"{5 + ci / 2}-{(char)('A' + ci % 2)}{(ci >= 52 ? ci.ToString() : "")}", 28);
            cls.MaxLessonsPerDay = periods;
            var whole = b.AddEntireClassGroup(cls);
            for (int si = 0; si < subjects.Length; si++)
            {
                var teacher = pool[si][ci % pool[si].Count];
                b.AddLesson(subjects[si], new[] { teacher }, new[] { whole }, hours[si]);
            }
        }
        return b.Build();
    }

    /// <summary>
    /// Katta stsenariy: 30 sinf, 150 guruh (har sinfda 5 guruh), ~1200 karta.
    /// </summary>
    public static Problem LargeSchool(int classCount = 30, int days = 5, int periods = 10)
    {
        var b = new ProblemBuilder(new TimeGrid(days, periods));
        var rng = new Xoshiro256SS(20240814);

        string[] fullSubjects = { "Matematika", "Ona tili", "Adabiyot", "Tarix", "Biologiya", "Fizika", "Kimyo", "Geografiya", "Informatika" };
        int[] fullHours = { 5, 4, 3, 2, 2, 3, 2, 2, 2 };
        string[] dividedSubjects = { "Ingliz tili", "Jismoniy tarbiya", "Mehnat" };
        int[] dividedHours = { 3, 2, 2 };
        int[] dividedTags = { 1, 2, 1 };

        var fullSubj = fullSubjects.Select(b.AddSubject).ToArray();
        var divSubj = dividedSubjects.Select(b.AddSubject).ToArray();

        // O'qituvchilar hovuzi: har fan uchun bir nechta.
        var pool = new Dictionary<string, List<TeacherDef>>();
        foreach (var name in fullSubjects.Concat(dividedSubjects))
        {
            var list = new List<TeacherDef>();
            int n = 8;
            for (int i = 0; i < n; i++)
            {
                var t = b.AddTeacher($"{name}-{i + 1}");
                t.MaxGapsPerDay = 3;
                t.MaxConsecutivePeriods = 5;
                list.Add(t);
            }
            pool[name] = list;
        }

        for (int ci = 0; ci < classCount; ci++)
        {
            var cls = b.AddClass($"Sinf-{ci + 1}", 30);
            cls.MaxLessonsPerDay = periods;
            var whole = b.AddEntireClassGroup(cls);
            var g1a = b.AddGroup(cls, $"Sinf-{ci + 1}/1a", 1, 15);
            var g1b = b.AddGroup(cls, $"Sinf-{ci + 1}/1b", 1, 15);
            var g2a = b.AddGroup(cls, $"Sinf-{ci + 1}/2a", 2, 16);
            var g2b = b.AddGroup(cls, $"Sinf-{ci + 1}/2b", 2, 14);

            for (int si = 0; si < fullSubj.Length; si++)
            {
                var t = pool[fullSubjects[si]][ci % pool[fullSubjects[si]].Count];
                b.AddLesson(fullSubj[si], new[] { t }, new[] { whole }, fullHours[si]);
            }

            for (int si = 0; si < divSubj.Length; si++)
            {
                var teachers = pool[dividedSubjects[si]];
                var ta = teachers[ci % teachers.Count];
                var tb = teachers[(ci + 3) % teachers.Count];
                var (ga, gb) = dividedTags[si] == 1 ? (g1a, g1b) : (g2a, g2b);
                b.AddLesson(divSubj[si], new[] { ta }, new[] { ga }, dividedHours[si]);
                b.AddLesson(divSubj[si], new[] { tb }, new[] { gb }, dividedHours[si]);
            }
        }
        _ = rng;
        return b.Build();
    }
}
