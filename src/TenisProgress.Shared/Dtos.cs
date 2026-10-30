using System.ComponentModel.DataAnnotations;

namespace TenisProgress.Shared;

// ---- Auth / account --------------------------------------------------------

public sealed record UserInfo(int Id, string Email, string Name, string Role)
{
    public bool IsCoach => Role == Roles.Coach;
}

public sealed record SetupStatus(bool NeedsSetup);

public sealed class LoginRequest
{
    [Required(ErrorMessage = "Podaj e-mail."), EmailAddress(ErrorMessage = "Niepoprawny e-mail.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Podaj hasło.")]
    public string Password { get; set; } = "";
}

public sealed class SetupRequest
{
    [Required(ErrorMessage = "Podaj imię i nazwisko."), StringLength(100)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Podaj e-mail."), EmailAddress(ErrorMessage = "Niepoprawny e-mail."), StringLength(200)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Podaj hasło."), MinLength(Validation.MinPassword, ErrorMessage = Validation.PasswordMessage), StringLength(200)]
    public string Password { get; set; } = "";
}

public sealed class ChangePasswordRequest
{
    [Required(ErrorMessage = "Podaj obecne hasło.")]
    public string Current { get; set; } = "";

    [Required(ErrorMessage = "Podaj nowe hasło."), MinLength(Validation.MinPassword, ErrorMessage = Validation.PasswordMessage), StringLength(200)]
    public string Password { get; set; } = "";

    [Compare(nameof(Password), ErrorMessage = "Hasła nie są identyczne.")]
    public string Password2 { get; set; } = "";
}

public static class Validation
{
    public const int MinPassword = 8;
    public const string PasswordMessage = "Hasło musi mieć co najmniej 8 znaków.";
}

// ---- Players -----------------------------------------------------------------

public sealed record ParentRef(int Id, string Name, string Email);

public sealed record PlayerSummary(
    int Id, string FirstName, string LastName, int? Age, string? Level, double? LatestAverage, IReadOnlyList<ParentRef>? Parents)
{
    public string FullName => $"{FirstName} {LastName}";
    public string Initials => $"{FirstName[..1]}{LastName[..1]}".ToUpperInvariant();
}

public sealed class PlayerForm
{
    [Required(ErrorMessage = "Podaj imię."), StringLength(80)]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "Podaj nazwisko."), StringLength(80)]
    public string LastName { get; set; } = "";

    public DateOnly? BirthDate { get; set; }

    [StringLength(60)]
    public string? Level { get; set; }

    public Hand? DominantHand { get; set; }

    [StringLength(4000)]
    public string? Notes { get; set; }

    [StringLength(4000)]
    public string? PrivateNotes { get; set; }
}

public enum Hand { Right, Left }

public sealed record AssessmentDto(int Id, DateOnly Date, IReadOnlyDictionary<string, int?> Scores, string? Comment)
{
    public double? Average
    {
        get
        {
            var values = Scores.Values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
            return values.Count == 0 ? null : values.Average();
        }
    }
}

public sealed record TrainingDto(int Id, DateOnly Date, string Topic, int? DurationMin, string? Notes);
public sealed record MatchDto(int Id, DateOnly Date, string? Tournament, string? Opponent, bool Won, string? Score, string? Notes);
public sealed record GoalDto(int Id, string Title, DateOnly? TargetDate, bool Done);

public sealed record PlayerDetail(
    int Id,
    string FirstName,
    string LastName,
    DateOnly? BirthDate,
    int? Age,
    string? Level,
    Hand? DominantHand,
    string? Notes,
    string? PrivateNotes,              // null for parents
    IReadOnlyList<ParentRef>? Parents, // null for parents
    IReadOnlyList<AssessmentDto> Assessments, // newest first
    IReadOnlyList<TrainingDto> Trainings,
    IReadOnlyList<MatchDto> Matches,
    IReadOnlyList<GoalDto> Goals)
{
    public string FullName => $"{FirstName} {LastName}";
    public string Initials => $"{FirstName[..1]}{LastName[..1]}".ToUpperInvariant();
}

public sealed class AssessmentForm : IValidatableObject
{
    [Required(ErrorMessage = "Podaj datę.")]
    public DateOnly? Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public Dictionary<string, int?> Scores { get; set; } = Skills.All.ToDictionary(s => s.Key, _ => (int?)null);

    [StringLength(2000)]
    public string? Comment { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Scores.Keys.Any(k => Skills.All.All(s => s.Key != k)))
            yield return new ValidationResult("Nieznana umiejętność.");
        if (Scores.Values.Any(v => v is < Skills.Min or > Skills.Max))
            yield return new ValidationResult("Oceny muszą być w skali 1–10.");
        if (Scores.Values.All(v => v is null))
            yield return new ValidationResult("Oceń przynajmniej jedną umiejętność.");
    }
}

public sealed class TrainingForm
{
    [Required(ErrorMessage = "Podaj datę.")]
    public DateOnly? Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Required(ErrorMessage = "Podaj temat treningu."), StringLength(200)]
    public string Topic { get; set; } = "";

    [Range(1, 600, ErrorMessage = "Czas 1–600 minut.")]
    public int? DurationMin { get; set; } = 60;

    [StringLength(2000)]
    public string? Notes { get; set; }
}

public sealed class MatchForm
{
    [Required(ErrorMessage = "Podaj datę.")]
    public DateOnly? Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [StringLength(200)]
    public string? Tournament { get; set; }

    [StringLength(200)]
    public string? Opponent { get; set; }

    public bool Won { get; set; } = true;

    [StringLength(60)]
    public string? Score { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }
}

public sealed class GoalForm
{
    [Required(ErrorMessage = "Podaj treść celu."), StringLength(300)]
    public string Title { get; set; } = "";

    public DateOnly? TargetDate { get; set; }
}

// ---- Parents -----------------------------------------------------------------

public sealed record ParentDto(int Id, string Name, string Email, string? Phone, IReadOnlyList<PlayerRef> Children);
public sealed record PlayerRef(int Id, string FullName);

public sealed class ParentForm
{
    [Required(ErrorMessage = "Podaj imię i nazwisko."), StringLength(100)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Podaj e-mail."), EmailAddress(ErrorMessage = "Niepoprawny e-mail."), StringLength(200)]
    public string Email { get; set; } = "";

    [StringLength(40)]
    public string? Phone { get; set; }

    /// <summary>Empty = generate a temporary password.</summary>
    [StringLength(200)]
    public string? Password { get; set; }

    public List<int> PlayerIds { get; set; } = [];
}

/// <summary>Returned once when an account is created or a password is reset.</summary>
public sealed record CreatedCredentials(int Id, string Email, string? TemporaryPassword);
