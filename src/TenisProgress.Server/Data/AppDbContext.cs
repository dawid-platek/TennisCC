using Microsoft.EntityFrameworkCore;
using TenisProgress.Server.Auth;

namespace TenisProgress.Server.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Training> Trainings => Set<Training>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<Goal> Goals => Set<Goal>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.Property(u => u.Email).UseCollation("NOCASE").HasMaxLength(200);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Name).HasMaxLength(100);
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(10);
        });

        b.Entity<Player>(e =>
        {
            e.Property(p => p.DominantHand).HasConversion<string>().HasMaxLength(10);
            e.HasMany(p => p.Parents).WithMany(u => u.Children).UsingEntity("PlayerParents");
            e.HasMany(p => p.Trainings).WithOne().HasForeignKey(t => t.PlayerId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Matches).WithOne().HasForeignKey(m => m.PlayerId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Goals).WithOne().HasForeignKey(g => g.PlayerId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Training>().HasIndex(t => new { t.PlayerId, t.Date });
        b.Entity<Match>().HasIndex(m => new { m.PlayerId, m.Date });
    }
}

public static class PlayerQueries
{
    /// <summary>
    /// The single access rule of the app: coaches see every player,
    /// parents only the children linked to their own account.
    /// </summary>
    public static IQueryable<Player> VisibleTo(this IQueryable<Player> players, CurrentUser user) =>
        user.IsCoach ? players : players.Where(p => p.Parents.Any(u => u.Id == user.Id));
}
