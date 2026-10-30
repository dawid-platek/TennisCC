using System.Security.Claims;
using TenisProgress.Server.Data;
using TenisProgress.Shared;

namespace TenisProgress.Server.Auth;

public sealed record CurrentUser(int Id, UserRole Role)
{
    public bool IsCoach => Role == UserRole.Coach;

    public static CurrentUser From(ClaimsPrincipal principal) => new(
        int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!),
        principal.IsInRole(Roles.Coach) ? UserRole.Coach : UserRole.Parent);
}
