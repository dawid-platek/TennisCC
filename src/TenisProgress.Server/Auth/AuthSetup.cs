using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TenisProgress.Server.Data;
using TenisProgress.Shared;

namespace TenisProgress.Server.Auth;

public static class AuthSetup
{
    public const string CoachPolicy = "Coach";
    public const string LoginRateLimit = "login";
    private const string StampClaim = "stamp";

    public static IServiceCollection AddAppAuth(this IServiceCollection services)
    {
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
                o.Cookie.Name = "tenis.auth";
                o.Cookie.HttpOnly = true;
                // Strict SameSite + JSON-only API is the CSRF protection for this app.
                o.Cookie.SameSite = SameSiteMode.Strict;
                o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                o.ExpireTimeSpan = TimeSpan.FromDays(14);
                o.SlidingExpiration = true;
                // An API answers with status codes instead of redirecting to a login page.
                o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
                o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
                o.Events.OnValidatePrincipal = ValidateStampAsync;
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(CoachPolicy, p => p.RequireRole(Roles.Coach));

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = 429;
            o.AddPolicy(LoginRateLimit, ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(15) }));
        });

        return services;
    }

    public static ClaimsPrincipal CreatePrincipal(User user)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Role, user.Role == UserRole.Coach ? Roles.Coach : Roles.Parent),
            new Claim(StampClaim, user.SecurityStamp),
        ], CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    // Rejects cookies of deleted users and of users whose password was changed/reset.
    private static async Task ValidateStampAsync(CookieValidatePrincipalContext ctx)
    {
        var id = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var stamp = ctx.Principal?.FindFirstValue(StampClaim);
        var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var valid = int.TryParse(id, out var userId) &&
            await db.Users.AnyAsync(u => u.Id == userId && u.SecurityStamp == stamp);
        if (!valid)
        {
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync();
        }
    }
}
