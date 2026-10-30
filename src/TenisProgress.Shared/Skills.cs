namespace TenisProgress.Shared;

public sealed record Skill(string Key, string Label);

public static class Skills
{
    /// <summary>Elements the coach rates (1–6) in every training entry.</summary>
    public static readonly IReadOnlyList<Skill> All =
    [
        new("forehand", "Forhend"),
        new("backhand", "Bekhend"),
        new("volley", "Wolej"),
        new("serve", "Serwis"),
        new("movement", "Poruszanie się"),
    ];

    public const int Min = 1;
    public const int Max = 6;

    /// <summary>
    /// Current level: the average of each element's most recent rating.
    /// Takes score sets newest first; an element missing from the latest training uses its earlier rating.
    /// </summary>
    public static double? LatestAverage(IEnumerable<IReadOnlyDictionary<string, int?>> newestFirst)
    {
        var sets = newestFirst.ToList();
        var latest = All
            .Select(skill => sets.Select(d => d.GetValueOrDefault(skill.Key)).FirstOrDefault(v => v.HasValue))
            .Where(v => v.HasValue).Select(v => v!.Value).ToList();
        return latest.Count == 0 ? null : latest.Average();
    }
}

public static class Roles
{
    public const string Coach = "Coach";
    public const string Parent = "Parent";
}
