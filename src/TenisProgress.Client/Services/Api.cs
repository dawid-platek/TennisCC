using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TenisProgress.Shared;

namespace TenisProgress.Client.Services;

/// <summary>Result of an API call: either a value or user-facing error messages.</summary>
public sealed record ApiResult<T>(T? Value, IReadOnlyList<string> Errors)
{
    public bool Ok => Errors.Count == 0;
}

/// <summary>Typed wrapper over the server API. Never throws for HTTP/network errors.</summary>
public sealed class Api(HttpClient http, AppAuthStateProvider auth)
{
    // ---- Auth / account ---------------------------------------------------
    public Task<ApiResult<SetupStatus>> SetupStatus() => Send<SetupStatus>(HttpMethod.Get, "api/setup");
    public Task<ApiResult<UserInfo>> Setup(SetupRequest r) => Send<UserInfo>(HttpMethod.Post, "api/setup", r);
    public Task<ApiResult<UserInfo>> Login(LoginRequest r) => Send<UserInfo>(HttpMethod.Post, "api/auth/login", r);
    public Task<ApiResult<object>> Logout() => Send<object>(HttpMethod.Post, "api/auth/logout");
    public Task<ApiResult<object>> ChangePassword(ChangePasswordRequest r) => Send<object>(HttpMethod.Post, "api/account/password", r);

    // ---- Players ------------------------------------------------------------
    public Task<ApiResult<List<PlayerSummary>>> Players() => Send<List<PlayerSummary>>(HttpMethod.Get, "api/players");
    public Task<ApiResult<PlayerDetail>> Player(int id) => Send<PlayerDetail>(HttpMethod.Get, $"api/players/{id}");
    public Task<ApiResult<IdResponse>> CreatePlayer(PlayerForm f) => Send<IdResponse>(HttpMethod.Post, "api/players", f);
    public Task<ApiResult<object>> UpdatePlayer(int id, PlayerForm f) => Send<object>(HttpMethod.Put, $"api/players/{id}", f);
    public Task<ApiResult<object>> DeletePlayer(int id) => Send<object>(HttpMethod.Delete, $"api/players/{id}");
    public Task<ApiResult<object>> LinkParent(int id, int parentId) => Send<object>(HttpMethod.Put, $"api/players/{id}/parents/{parentId}");
    public Task<ApiResult<object>> UnlinkParent(int id, int parentId) => Send<object>(HttpMethod.Delete, $"api/players/{id}/parents/{parentId}");

    public Task<ApiResult<object>> AddRecord(int id, string kind, object form) => Send<object>(HttpMethod.Post, $"api/players/{id}/{kind}", form);
    public Task<ApiResult<object>> DeleteRecord(int id, string kind, int recordId) => Send<object>(HttpMethod.Delete, $"api/players/{id}/{kind}/{recordId}");
    public Task<ApiResult<object>> ToggleGoal(int id, int goalId) => Send<object>(HttpMethod.Post, $"api/players/{id}/goals/{goalId}/toggle");

    // ---- Parents ------------------------------------------------------------
    public Task<ApiResult<List<ParentDto>>> Parents() => Send<List<ParentDto>>(HttpMethod.Get, "api/parents");
    public Task<ApiResult<CreatedCredentials>> CreateParent(ParentForm f) => Send<CreatedCredentials>(HttpMethod.Post, "api/parents", f);
    public Task<ApiResult<CreatedCredentials>> ResetParentPassword(int id) => Send<CreatedCredentials>(HttpMethod.Post, $"api/parents/{id}/reset-password");
    public Task<ApiResult<object>> DeleteParent(int id) => Send<object>(HttpMethod.Delete, $"api/parents/{id}");

    public sealed record IdResponse(int Id);

    private async Task<ApiResult<T>> Send<T>(HttpMethod method, string url, object? body = null)
    {
        HttpResponseMessage res;
        try
        {
            var req = new HttpRequestMessage(method, url);
            if (body is not null) req.Content = JsonContent.Create(body, body.GetType());
            res = await http.SendAsync(req);
        }
        catch (HttpRequestException)
        {
            return Fail<T>("Brak połączenia z serwerem. Sprawdź internet i spróbuj ponownie.");
        }

        if (res.IsSuccessStatusCode)
        {
            var hasBody = res.StatusCode != HttpStatusCode.NoContent && typeof(T) != typeof(object);
            return new ApiResult<T>(hasBody ? await res.Content.ReadFromJsonAsync<T>() : default, []);
        }

        switch (res.StatusCode)
        {
            case HttpStatusCode.Unauthorized when !url.StartsWith("api/auth/login"):
                auth.SignedOut(); // session expired or password reset -> back to login
                return Fail<T>("Sesja wygasła. Zaloguj się ponownie.");
            case HttpStatusCode.Forbidden:
                return Fail<T>("Nie masz uprawnień do tej operacji.");
            case HttpStatusCode.NotFound:
                return Fail<T>("Nie znaleziono.");
            case HttpStatusCode.TooManyRequests:
                return Fail<T>("Zbyt wiele prób. Spróbuj ponownie za kilkanaście minut.");
        }
        return new ApiResult<T>(default, await ReadProblemAsync(res));
    }

    // Turns ASP.NET Core ProblemDetails / ValidationProblemDetails into messages.
    private static async Task<IReadOnlyList<string>> ReadProblemAsync(HttpResponseMessage res)
    {
        try
        {
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                var list = errors.EnumerateObject().SelectMany(p => p.Value.EnumerateArray()).Select(e => e.GetString() ?? "").ToList();
                if (list.Count > 0) return list;
            }
            if (root.TryGetProperty("detail", out var detail) && detail.GetString() is { Length: > 0 } d) return [d];
        }
        catch (JsonException) { }
        return ["Wystąpił nieoczekiwany błąd serwera."];
    }

    private static ApiResult<T> Fail<T>(string message) => new(default, [message]);
}
