using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TenisProgress.Server.Auth;
using TenisProgress.Server.Data;
using TenisProgress.Shared;

namespace TenisProgress.Server.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapSetup();

        api.MapPost("/auth/login", async (LoginRequest req, AppDbContext db, IPasswordHasher<User> hasher, HttpContext http) =>
        {
            var user = await db.Users.SingleOrDefaultAsync(u => u.Email == req.Email.Trim());
            var result = user is null
                ? PasswordVerificationResult.Failed
                : hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password);
            if (user is null || result == PasswordVerificationResult.Failed)
                return Results.Problem("Nieprawidłowy e-mail lub hasło.", statusCode: 401);

            if (result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordHash = hasher.HashPassword(user, req.Password);
                await db.SaveChangesAsync();
            }
            await http.SignInAsync(AuthSetup.CreatePrincipal(user));
            return Results.Ok(ToInfo(user));
        }).Validate<LoginRequest>().RequireRateLimiting(AuthSetup.LoginRateLimit);

        api.MapPost("/auth/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync();
            return Results.NoContent();
        });

        api.MapGet("/auth/me", async (ClaimsPrincipal principal, AppDbContext db) =>
        {
            var user = await db.Users.FindAsync(CurrentUser.From(principal).Id);
            return user is null ? Results.Unauthorized() : Results.Ok(ToInfo(user));
        }).RequireAuthorization();

        api.MapPost("/account/password", async (ChangePasswordRequest req, ClaimsPrincipal principal, AppDbContext db,
            IPasswordHasher<User> hasher, HttpContext http) =>
        {
            var user = await db.Users.FindAsync(CurrentUser.From(principal).Id);
            if (user is null) return Results.Unauthorized();
            if (hasher.VerifyHashedPassword(user, user.PasswordHash, req.Current) == PasswordVerificationResult.Failed)
                return ValidationExtensions.Problem("Obecne hasło jest nieprawidłowe.");

            user.PasswordHash = hasher.HashPassword(user, req.Password);
            user.SecurityStamp = Guid.NewGuid().ToString("N"); // logs out other devices
            await db.SaveChangesAsync();
            await http.SignInAsync(AuthSetup.CreatePrincipal(user));
            return Results.NoContent();
        }).Validate<ChangePasswordRequest>().RequireAuthorization();
    }

    // First-run setup: creating the first coach is only possible while there are no users.
    private static void MapSetup(this RouteGroupBuilder api)
    {
        api.MapGet("/setup", async (AppDbContext db) => new SetupStatus(!await db.Users.AnyAsync()));

        api.MapPost("/setup", async (SetupRequest req, AppDbContext db, IPasswordHasher<User> hasher, HttpContext http) =>
        {
            if (await db.Users.AnyAsync()) return Results.Forbid();
            var user = new User { Name = req.Name.Trim(), Email = req.Email.Trim(), Role = UserRole.Coach };
            user.PasswordHash = hasher.HashPassword(user, req.Password);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            await http.SignInAsync(AuthSetup.CreatePrincipal(user));
            return Results.Ok(ToInfo(user));
        }).Validate<SetupRequest>().RequireRateLimiting(AuthSetup.LoginRateLimit);
    }

    public static UserInfo ToInfo(User u) => new(u.Id, u.Email, u.Name, u.Role == UserRole.Coach ? Roles.Coach : Roles.Parent);
}
