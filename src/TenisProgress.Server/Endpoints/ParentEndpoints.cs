using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TenisProgress.Server.Auth;
using TenisProgress.Server.Data;
using TenisProgress.Shared;

namespace TenisProgress.Server.Endpoints;

public static class ParentEndpoints
{
    public static void MapParentEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/parents").RequireAuthorization(AuthSetup.CoachPolicy);

        api.MapGet("/", async (AppDbContext db) =>
            await db.Users.Where(u => u.Role == UserRole.Parent).OrderBy(u => u.Name)
                .Select(u => new ParentDto(u.Id, u.Name, u.Email, u.Phone,
                    u.Children.OrderBy(c => c.FirstName)
                        .Select(c => new PlayerRef(c.Id, c.FirstName + " " + c.LastName)).ToList()))
                .AsNoTracking().ToListAsync());

        api.MapPost("/", async (ParentForm form, AppDbContext db, IPasswordHasher<User> hasher) =>
        {
            var email = form.Email.Trim();
            if (await db.Users.AnyAsync(u => u.Email == email))
                return ValidationExtensions.Problem("Konto z tym adresem e-mail już istnieje.");

            var password = form.Password.Clean();
            if (password is { Length: < Validation.MinPassword }) return ValidationExtensions.Problem(Validation.PasswordMessage);
            var generated = password is null;
            password ??= TemporaryPassword();

            var parent = new User { Name = form.Name.Trim(), Email = email, Phone = form.Phone.Clean(), Role = UserRole.Parent };
            parent.PasswordHash = hasher.HashPassword(parent, password);
            parent.Children = await db.Players.Where(p => form.PlayerIds.Contains(p.Id)).ToListAsync();
            db.Users.Add(parent);
            await db.SaveChangesAsync();
            return Results.Ok(new CreatedCredentials(parent.Id, email, generated ? password : null));
        }).Validate<ParentForm>();

        api.MapPost("/{id:int}/reset-password", async (int id, AppDbContext db, IPasswordHasher<User> hasher) =>
        {
            var parent = await db.Users.SingleOrDefaultAsync(u => u.Id == id && u.Role == UserRole.Parent);
            if (parent is null) return Results.NotFound();
            var password = TemporaryPassword();
            parent.PasswordHash = hasher.HashPassword(parent, password);
            parent.SecurityStamp = Guid.NewGuid().ToString("N"); // logs the parent out everywhere
            await db.SaveChangesAsync();
            return Results.Ok(new CreatedCredentials(parent.Id, parent.Email, password));
        });

        api.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var parent = await db.Users.Include(u => u.Children).SingleOrDefaultAsync(u => u.Id == id && u.Role == UserRole.Parent);
            if (parent is null) return Results.NotFound();
            db.Users.Remove(parent);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    /// <summary>Readable temporary password, e.g. "kX7m-p2Qa-9TzR".</summary>
    public static string TemporaryPassword()
    {
        const string alphabet = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var chars = RandomNumberGenerator.GetItems<char>(alphabet, 12);
        return $"{new string(chars, 0, 4)}-{new string(chars, 4, 4)}-{new string(chars, 8, 4)}";
    }
}
