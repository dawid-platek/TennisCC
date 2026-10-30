namespace TenisProgress.Shared;

public sealed record Skill(string Key, string Label);

public static class Skills
{
    public static readonly IReadOnlyList<Skill> All =
    [
        new("forehand", "Forhend"),
        new("backhand", "Bekhend"),
        new("serve", "Serwis"),
        new("volley", "Wolej"),
        new("returnServe", "Return"),
        new("footwork", "Praca nóg"),
        new("tactics", "Taktyka"),
        new("mental", "Psychika"),
    ];

    public const int Min = 1;
    public const int Max = 10;
}

public static class Roles
{
    public const string Coach = "Coach";
    public const string Parent = "Parent";
}
