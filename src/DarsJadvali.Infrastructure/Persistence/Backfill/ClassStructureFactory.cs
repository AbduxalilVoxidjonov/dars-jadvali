using DarsJadvali.Domain.Entities;

namespace DarsJadvali.Infrastructure.Persistence.Backfill;

/// <summary>
/// Sinf uchun standart bo'linishlar va guruhlarni yaratadi.
/// </summary>
/// <remarks>
/// aSc'dagi standart 5 bo'linish/guruh sxemasi (tasdiqlangan talab: sinfiga <b>aniq 5 ta</b>
/// guruh, 30 sinf × 5 = 150 guruh):
/// <list type="table">
/// <item><term><c>tag = 0</c></term><description>"Butun sinf" — 1 guruh
/// (<c>IsEntireClass = true</c>)</description></item>
/// <item><term><c>tag = 1</c></term><description>"Guruhlar" — "1-guruh", "2-guruh"</description></item>
/// <item><term><c>tag = 2</c></term><description>"O'g'il/qiz" — "O'g'illar", "Qizlar"</description></item>
/// </list>
/// Qoida: bir vaqtda dars o'tishi mumkin bo'lgan guruhlar — faqat <b>bitta</b> bo'linish
/// ichidagi turli guruhlar.
/// </remarks>
public static class ClassStructureFactory
{
    /// <summary>Butun sinf bo'linishi tegi.</summary>
    public const int TagEntireClass = 0;

    /// <summary>1/2 guruh bo'linishi tegi.</summary>
    public const int TagHalves = 1;

    /// <summary>O'g'il/qiz bo'linishi tegi.</summary>
    public const int TagGender = 2;

    /// <summary>"Butun sinf" guruhining nomi.</summary>
    public const string EntireClassGroupName = "Butun sinf";

    /// <summary>Har sinfda yaratiladigan guruhlar soni.</summary>
    public const int GroupsPerClass = 5;

    /// <summary>
    /// Sinf uchun standart bo'linishlar va guruhlarni <paramref name="context"/> ga qo'shadi.
    /// Mavjud bo'linishlarni takrorlamaydi (idempotent).
    /// </summary>
    /// <returns>Yaratilgan guruhlar soni.</returns>
    public static int AddStandardStructure(
        AppDbContext context,
        SchoolClass schoolClass,
        IReadOnlyCollection<int> existingTags)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(schoolClass);

        var created = 0;

        foreach (var (tag, divisionName, groupNames) in StandardDivisions)
        {
            if (existingTags.Contains(tag)) continue;

            var division = new ClassDivision
            {
                SchoolClass = schoolClass,
                DivisionTag = tag,
                Name = divisionName
            };
            context.ClassDivisions.Add(division);

            foreach (var groupName in groupNames)
            {
                context.StudentGroups.Add(new StudentGroup
                {
                    SchoolClass = schoolClass,
                    ClassDivision = division,
                    Name = groupName,
                    IsEntireClass = tag == TagEntireClass
                });
                created++;
            }
        }

        return created;
    }

    private static readonly (int Tag, string Name, string[] Groups)[] StandardDivisions =
    {
        (TagEntireClass, "Butun sinf", new[] { EntireClassGroupName }),
        (TagHalves, "Guruhlar", new[] { "1-guruh", "2-guruh" }),
        (TagGender, "O'g'il/qiz", new[] { "O'g'illar", "Qizlar" })
    };
}
