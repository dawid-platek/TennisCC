using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using TenisProgress.Shared;

namespace TenisProgress.Client.Services;

/// <summary>
/// Knows who is logged in by asking the server (/api/auth/me). The auth cookie itself is
/// HttpOnly, so the browser app never sees it — the server stays the source of truth.
/// </summary>
public sealed class AppAuthStateProvider(HttpClient http) : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));
    private Task<AuthenticationState>? _state;

    public UserInfo? User { get; private set; }

    /// <summary>True when the server couldn't be reached at startup (e.g. installed app opened offline).</summary>
    public bool IsOffline { get; private set; }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => _state ??= LoadAsync();

    private async Task<AuthenticationState> LoadAsync()
    {
        IsOffline = false;
        try
        {
            var res = await http.GetAsync("api/auth/me");
            if (res.StatusCode == HttpStatusCode.Unauthorized) return SetUser(null);
            res.EnsureSuccessStatusCode();
            return SetUser(await res.Content.ReadFromJsonAsync<UserInfo>());
        }
        catch (HttpRequestException)
        {
            IsOffline = true;
            return SetUser(null);
        }
    }

    public void Retry() => NotifyAuthenticationStateChanged(_state = LoadAsync());

    public void SignedIn(UserInfo user) => NotifyAuthenticationStateChanged(_state = Task.FromResult(SetUser(user)));

    public void SignedOut() => NotifyAuthenticationStateChanged(_state = Task.FromResult(SetUser(null)));

    private AuthenticationState SetUser(UserInfo? user)
    {
        User = user;
        if (user is null) return Anonymous;
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
        ], authenticationType: "cookie");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }
}
