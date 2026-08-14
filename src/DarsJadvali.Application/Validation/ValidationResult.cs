using System.Text;

namespace DarsJadvali.Application.Validation;

/// <summary>Validatsiya natijasi.</summary>
public sealed class ValidationResult
{
    private static readonly ValidationResult Ok = new(Array.Empty<Conflict>());

    private ValidationResult(IReadOnlyList<Conflict> conflicts)
    {
        Conflicts = conflicts;
    }

    /// <summary>Topilgan konfliktlar.</summary>
    public IReadOnlyList<Conflict> Conflicts { get; }

    /// <summary>Error darajali konflikt yo'qligini bildiradi.</summary>
    public bool IsValid => !Conflicts.Any(c => c.Severity == ConflictSeverity.Error);

    /// <summary>Ogohlantirishlar bor-yo'qligi.</summary>
    public bool HasWarnings => Conflicts.Any(c => c.Severity == ConflictSeverity.Warning);

    /// <summary>Konfliktsiz natija.</summary>
    public static ValidationResult Success() => Ok;

    /// <summary>Konfliktlar ro'yxatidan natija yasaydi.</summary>
    public static ValidationResult From(IEnumerable<Conflict> conflicts)
    {
        ArgumentNullException.ThrowIfNull(conflicts);
        var list = conflicts.ToList();
        return list.Count == 0 ? Ok : new ValidationResult(list);
    }

    /// <summary>Konfliktlarni foydalanuvchiga ko'rsatiladigan matnga aylantiradi.</summary>
    public string ToDisplayText()
    {
        if (Conflicts.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        for (var i = 0; i < Conflicts.Count; i++)
        {
            if (i > 0)
            {
                sb.AppendLine();
            }

            sb.Append("• ").Append(Conflicts[i].Message);
        }

        return sb.ToString();
    }
}
