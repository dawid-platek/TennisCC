using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TenisProgress.Server.Auth;
using TenisProgress.Server.Data;
using TenisProgress.Shared;

namespace TenisProgress.Server.Endpoints;

public static class PlayerEndpoints
{
    public static void MapPlayerEndpoints(this IEndpointRouteBuilder app)
    {
        // ---- Read: coaches and the player's own parents ---------------------

        var read = app.MapGroup("/api/players").RequireAuthorization();

        read.MapGet("/", async (ClaimsPrincipal principal, AppDbContext db) =>
        {
            var user = CurrentUser.From(principal);
            var players = await db.Players.VisibleTo(user)
                .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
                .Select(p => new
                {
                    Player = p,
                    Latest = p.Assessments.OrderByDescending(a => a.Date).ThenByDescending(a => a.Id).FirstOrDefault(),
                    Parents = p.Parents.OrderBy(u => u.Name).Select(u => new ParentRef(u.Id, u.Name, u.Email)).ToList(),
                })
                .AsNoTracking().ToListAsync();

            return players.Select(x => new PlayerSummary(
                x.Player.Id, x.Player.FirstName, x.Player.LastName, Age(x.Player.BirthDate), x.Player.Level,
                x.Latest is null ? null : Average(x.Latest),
                user.IsCoach ? x.Parents : null));
        });

        read.MapGet("/{id:int}", async (int id, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var user = CurrentUser.From(principal);
            // Same 404 for "doesn't exist" and "not your child", so ids can't be probed.
            var p = await db.Players.VisibleTo(user)
                .Include(p => p.Assessments).Include(p => p.Trainings).Include(p => p.Matches)
                .Include(p => p.Goals).Include(p => p.Parents)
                .AsSplitQuery().AsNoTracking()
                .SingleOrDefaultAsync(p => p.Id == id);
            if (p is null) return Results.NotFound();

            return Results.Ok(new PlayerDetail(
                p.Id, p.FirstName, p.LastName, p.BirthDate, Age(p.BirthDate), p.Level, p.DominantHand, p.Notes,
                user.IsCoach ? p.PrivateNotes : null,
                user.IsCoach ? p.Parents.OrderBy(u => u.Name).Select(u => new ParentRef(u.Id, u.Name, u.Email)).ToList() : null,
                p.Assessments.OrderByDescending(a => a.Date).ThenByDescending(a => a.Id)
                    .Select(a => new AssessmentDto(a.Id, a.Date, a.GetScores(), a.Comment)).ToList(),
                p.Trainings.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id)
                    .Select(t => new TrainingDto(t.Id, t.Date, t.Topic, t.DurationMin, t.Notes)).ToList(),
                p.Matches.OrderByDescending(m => m.Date).ThenByDescending(m => m.Id)
                    .Select(m => new MatchDto(m.Id, m.Date, m.Tournament, m.Opponent, m.Won, m.Score, m.Notes)).ToList(),
                p.Goals.OrderBy(g => g.Done).ThenBy(g => g.TargetDate == null).ThenBy(g => g.TargetDate).ThenBy(g => g.Id)
                    .Select(g => new GoalDto(g.Id, g.Title, g.TargetDate, g.Done)).ToList()));
        });

        // ---- Write: coaches only -------------------------------------------

        var coach = app.MapGroup("/api/players").RequireAuthorization(AuthSetup.CoachPolicy);

        coach.MapPost("/", async (PlayerForm form, AppDbContext db) =>
        {
            var player = new Player();
            Apply(player, form);
            db.Players.Add(player);
            await db.SaveChangesAsync();
            return Results.Created($"/api/players/{player.Id}", new { player.Id });
        }).Validate<PlayerForm>();

        coach.MapPut("/{id:int}", async (int id, PlayerForm form, AppDbContext db) =>
        {
            var player = await db.Players.FindAsync(id);
            if (player is null) return Results.NotFound();
            Apply(player, form);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).Validate<PlayerForm>();

        coach.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
            await db.Players.Where(p => p.Id == id).ExecuteDeleteAsync() > 0 ? Results.NoContent() : Results.NotFound());

        coach.MapPut("/{id:int}/parents/{parentId:int}", async (int id, int parentId, AppDbContext db) =>
        {
            var player = await db.Players.Include(p => p.Parents).SingleOrDefaultAsync(p => p.Id == id);
            var parent = await db.Users.SingleOrDefaultAsync(u => u.Id == parentId && u.Role == UserRole.Parent);
            if (player is null || parent is null) return Results.NotFound();
            if (!player.Parents.Contains(parent)) player.Parents.Add(parent);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        coach.MapDelete("/{id:int}/parents/{parentId:int}", async (int id, int parentId, AppDbContext db) =>
        {
            var player = await db.Players.Include(p => p.Parents).SingleOrDefaultAsync(p => p.Id == id);
            if (player is null) return Results.NotFound();
            player.Parents.RemoveAll(u => u.Id == parentId);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        coach.MapPost("/{id:int}/assessments", (int id, AssessmentForm f, AppDbContext db) =>
        {
            var a = new Assessment { PlayerId = id, Date = f.Date!.Value, Comment = f.Comment.Clean() };
            a.SetScores(f.Scores);
            return AddRecord(db, id, a);
        }).Validate<AssessmentForm>();

        coach.MapPost("/{id:int}/trainings", (int id, TrainingForm f, AppDbContext db) =>
            AddRecord(db, id, new Training
            {
                PlayerId = id, Date = f.Date!.Value, Topic = f.Topic.Trim(), DurationMin = f.DurationMin, Notes = f.Notes.Clean(),
            })).Validate<TrainingForm>();

        coach.MapPost("/{id:int}/matches", (int id, MatchForm f, AppDbContext db) =>
            AddRecord(db, id, new Match
            {
                PlayerId = id, Date = f.Date!.Value, Tournament = f.Tournament.Clean(), Opponent = f.Opponent.Clean(),
                Won = f.Won, Score = f.Score.Clean(), Notes = f.Notes.Clean(),
            })).Validate<MatchForm>();

        coach.MapPost("/{id:int}/goals", (int id, GoalForm f, AppDbContext db) =>
            AddRecord(db, id, new Goal { PlayerId = id, Title = f.Title.Trim(), TargetDate = f.TargetDate }))
            .Validate<GoalForm>();

        coach.MapPost("/{id:int}/goals/{goalId:int}/toggle", async (int id, int goalId, AppDbContext db) =>
            await db.Goals.Where(g => g.Id == goalId && g.PlayerId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(g => g.Done, g => !g.Done)) > 0
                ? Results.NoContent() : Results.NotFound());

        coach.MapDelete("/{id:int}/assessments/{rid:int}", (int id, int rid, AppDbContext db) => DeleteRecord(db.Assessments, id, rid));
        coach.MapDelete("/{id:int}/trainings/{rid:int}", (int id, int rid, AppDbContext db) => DeleteRecord(db.Trainings, id, rid));
        coach.MapDelete("/{id:int}/matches/{rid:int}", (int id, int rid, AppDbContext db) => DeleteRecord(db.Matches, id, rid));
        coach.MapDelete("/{id:int}/goals/{rid:int}", (int id, int rid, AppDbContext db) => DeleteRecord(db.Goals, id, rid));
    }

    private static async Task<IResult> AddRecord<T>(AppDbContext db, int playerId, T record) where T : class
    {
        if (!await db.Players.AnyAsync(p => p.Id == playerId)) return Results.NotFound();
        db.Add(record);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteRecord<T>(DbSet<T> set, int playerId, int id) where T : class =>
        await set.Where(r => EF.Property<int>(r, "Id") == id && EF.Property<int>(r, "PlayerId") == playerId)
            .ExecuteDeleteAsync() > 0 ? Results.NoContent() : Results.NotFound();

    private static void Apply(Player p, PlayerForm f)
    {
        p.FirstName = f.FirstName.Trim();
        p.LastName = f.LastName.Trim();
        p.BirthDate = f.BirthDate;
        p.Level = f.Level.Clean();
        p.DominantHand = f.DominantHand;
        p.Notes = f.Notes.Clean();
        p.PrivateNotes = f.PrivateNotes.Clean();
    }

    private static double? Average(Assessment a)
    {
        var values = a.GetScores().Values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        return values.Count == 0 ? null : values.Average();
    }

    public static int? Age(DateOnly? birth)
    {
        if (birth is not { } b) return null;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var age = today.Year - b.Year;
        return b.AddYears(age) > today ? age - 1 : age;
    }
}
