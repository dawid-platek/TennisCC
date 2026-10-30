using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TenisProgress.Server.Data;
using TenisProgress.Shared;

namespace TenisProgress.Server;

public static class Commands
{
    public static async Task<bool> TryRunAsync(string[] args, IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        switch (args[0])
        {
            case "create-coach":
                await CreateCoachAsync(db, hasher, args[1..]);
                return true;
            case "seed":
                await SeedAsync(db, hasher);
                return true;
            default:
                return false;
        }
    }

    // dotnet run -- create-coach "Jan Kowalski" jan@example.com haslo1234
    private static async Task CreateCoachAsync(AppDbContext db, IPasswordHasher<User> hasher, string[] a)
    {
        if (a.Length != 3 || a[2].Length < Validation.MinPassword)
        {
            Console.Error.WriteLine("Użycie: create-coach \"Imię Nazwisko\" email@example.com haslo (min. 8 znaków)");
            Environment.ExitCode = 1;
            return;
        }
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == a[1]);
        var created = user is null;
        user ??= db.Users.Add(new User { Email = a[1] }).Entity;
        user.Name = a[0];
        user.Role = UserRole.Coach;
        user.PasswordHash = hasher.HashPassword(user, a[2]);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync();
        Console.WriteLine(created ? $"Utworzono konto trenera: {a[1]}" : $"Zaktualizowano konto trenera: {a[1]}");
    }

    // Demo data: one coach, two parents, three kids. Only for an empty database.
    private static async Task SeedAsync(AppDbContext db, IPasswordHasher<User> hasher)
    {
        if (await db.Users.AnyAsync())
        {
            Console.Error.WriteLine("Baza nie jest pusta — pomijam dane demonstracyjne.");
            Environment.ExitCode = 1;
            return;
        }

        User NewUser(string email, string name, UserRole role)
        {
            var u = new User { Email = email, Name = name, Role = role };
            u.PasswordHash = hasher.HashPassword(u, "demo1234");
            return u;
        }

        var anna = NewUser("anna@demo.pl", "Anna Nowak", UserRole.Parent);
        var piotr = NewUser("piotr@demo.pl", "Piotr Wiśniewski", UserRole.Parent);
        db.Users.AddRange(NewUser("trener@demo.pl", "Trener Demo", UserRole.Coach), anna, piotr);

        var rnd = new Random(7);
        (string First, string Last, DateOnly Birth, string Level, Hand Hand, User Parent)[] kids =
        [
            ("Zosia", "Nowak", new(2015, 4, 12), "Green", Hand.Right, anna),
            ("Kuba", "Nowak", new(2017, 9, 3), "Orange", Hand.Left, anna),
            ("Maja", "Wiśniewska", new(2014, 1, 25), "Yellow", Hand.Right, piotr),
        ];
        foreach (var k in kids)
        {
            var player = new Player
            {
                FirstName = k.First, LastName = k.Last, BirthDate = k.Birth, Level = k.Level, DominantHand = k.Hand,
                Notes = "Bardzo zaangażowany zawodnik, regularna frekwencja.", Parents = [k.Parent],
            };
            var start = 3 + rnd.Next(2);
            for (var i = 0; i < 4; i++)
            {
                var a = new Assessment
                {
                    Date = new DateOnly(2026, 3 + 2 * i, 1),
                    Comment = i == 3 ? "Wyraźny postęp w regularności wymian." : null,
                };
                a.SetScores(Skills.All.ToDictionary(s => s.Key, _ => (int?)Math.Min(10, start + i + rnd.Next(2))));
                player.Assessments.Add(a);
            }
            player.Trainings.Add(new Training { Date = new(2026, 9, 10), Topic = "Serwis – rzut piłki i rytm", DurationMin = 60 });
            player.Matches.Add(new Match { Date = new(2026, 9, 20), Tournament = "Turniej Klubowy", Opponent = "J. Kowalski", Won = true, Score = "6:3 6:4" });
            player.Goals.Add(new Goal { Title = "20 wymian z rzędu z linii końcowej", TargetDate = new(2026, 12, 31) });
            db.Players.Add(player);
        }
        await db.SaveChangesAsync();
        Console.WriteLine("Dane demo gotowe. Loginy (hasło: demo1234): trener@demo.pl, anna@demo.pl, piotr@demo.pl");
    }
}
