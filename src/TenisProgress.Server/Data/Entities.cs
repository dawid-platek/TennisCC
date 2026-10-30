using TenisProgress.Shared;

namespace TenisProgress.Server.Data;

public enum UserRole { Coach, Parent }

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; }

    /// <summary>Changes on password reset; invalidates every existing login cookie.</summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<Player> Children { get; set; } = [];
}

public class Player
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateOnly? BirthDate { get; set; }
    public string? Level { get; set; }
    public Hand? DominantHand { get; set; }
    public string? Notes { get; set; }
    public string? PrivateNotes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<User> Parents { get; set; } = [];
    public List<Training> Trainings { get; set; } = [];
    public List<Match> Matches { get; set; } = [];
    public List<Goal> Goals { get; set; } = [];
}

public class Training
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public DateOnly Date { get; set; }
    public string Topic { get; set; } = "";
    public int? DurationMin { get; set; }
    public string? Notes { get; set; }

    // Ratings 1–6 per element; null = not rated in this training.
    public int? Forehand { get; set; }
    public int? Backhand { get; set; }
    public int? Volley { get; set; }
    public int? Serve { get; set; }
    public int? Movement { get; set; }

    // Maps the shared skill keys (Skills.All) onto the columns above.
    public Dictionary<string, int?> GetScores() => new()
    {
        ["forehand"] = Forehand, ["backhand"] = Backhand, ["volley"] = Volley, ["serve"] = Serve, ["movement"] = Movement,
    };

    public void SetScores(IReadOnlyDictionary<string, int?> s)
    {
        int? Get(string key) => s.TryGetValue(key, out var v) ? v : null;
        Forehand = Get("forehand"); Backhand = Get("backhand"); Volley = Get("volley");
        Serve = Get("serve"); Movement = Get("movement");
    }
}

public class Match
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public DateOnly Date { get; set; }
    public string? Tournament { get; set; }
    public string? Opponent { get; set; }
    public bool Won { get; set; }
    public string? Score { get; set; }
    public string? Notes { get; set; }
}

public class Goal
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public string Title { get; set; } = "";
    public DateOnly? TargetDate { get; set; }
    public bool Done { get; set; }
}
