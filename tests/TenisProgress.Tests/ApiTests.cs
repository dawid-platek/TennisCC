using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using TenisProgress.Shared;

namespace TenisProgress.Tests;

public sealed class AppFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"tenis-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath};Pooling=False");
        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        foreach (var f in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" }) File.Delete(f);
    }
}

/// <summary>
/// One scenario shared by all tests (ordered): a coach sets the app up, adds
/// three kids and two parents, then each parent must see only their own kids.
/// </summary>
[TestCaseOrderer("TenisProgress.Tests.AlphabeticalOrderer", "TenisProgress.Tests")]
public sealed class ApiTests(ApiTests.Scenario s) : IClassFixture<ApiTests.Scenario>
{
    public sealed class Scenario : IAsyncLifetime
    {
        public AppFactory Factory { get; } = new();
        public HttpClient Coach = null!, Anna = null!, Piotr = null!;
        public int Zosia, Kuba, Maja, AnnaId;

        public async Task InitializeAsync()
        {
            Coach = NewClient();
            Assert.True((await Coach.GetFromJsonAsync<SetupStatus>("/api/setup"))!.NeedsSetup);
            (await Coach.PostAsJsonAsync("/api/setup", new SetupRequest { Name = "Trener", Email = "coach@test.pl", Password = "tajnehaslo" }))
                .EnsureSuccessStatusCode();

            Zosia = await AddPlayer("Zosia", "Nowak");
            Kuba = await AddPlayer("Kuba", "Nowak");
            Maja = await AddPlayer("Maja", "Wiśniewska");

            var anna = await (await Coach.PostAsJsonAsync("/api/parents",
                new ParentForm { Name = "Anna Nowak", Email = "anna@test.pl", PlayerIds = [Zosia, Kuba] }))
                .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<CreatedCredentials>();
            AnnaId = anna!.Id;
            var piotr = await (await Coach.PostAsJsonAsync("/api/parents",
                new ParentForm { Name = "Piotr W", Email = "piotr@test.pl", Password = "piotrhaslo" }))
                .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<CreatedCredentials>();
            (await Coach.PutAsync($"/api/players/{Maja}/parents/{piotr!.Id}", null)).EnsureSuccessStatusCode();

            var assessment = new AssessmentForm { Date = new DateOnly(2026, 9, 1), Comment = "Dobry trening" };
            assessment.Scores["forehand"] = 6;
            assessment.Scores["serve"] = 4;
            (await Coach.PostAsJsonAsync($"/api/players/{Zosia}/assessments", assessment)).EnsureSuccessStatusCode();

            Anna = await Login("anna@test.pl", anna.TemporaryPassword!);
            Piotr = await Login("piotr@test.pl", "piotrhaslo");
        }

        public HttpClient NewClient() => Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        public async Task<HttpClient> Login(string email, string password)
        {
            var c = NewClient();
            (await c.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password })).EnsureSuccessStatusCode();
            return c;
        }

        private async Task<int> AddPlayer(string first, string last)
        {
            var res = await Coach.PostAsJsonAsync("/api/players",
                new PlayerForm { FirstName = first, LastName = last, BirthDate = new DateOnly(2015, 5, 5), PrivateNotes = "SEKRET-TRENERA" });
            res.EnsureSuccessStatusCode();
            return (await res.Content.ReadFromJsonAsync<IdResponse>())!.Id;
        }

        public Task DisposeAsync()
        {
            Factory.Dispose();
            return Task.CompletedTask;
        }
    }

    private sealed record IdResponse(int Id);

    [Fact]
    public async Task A_setup_is_locked_after_first_coach()
    {
        Assert.False((await s.Coach.GetFromJsonAsync<SetupStatus>("/api/setup"))!.NeedsSetup);
        var res = await s.NewClient().PostAsJsonAsync("/api/setup", new SetupRequest { Name = "X", Email = "x@test.pl", Password = "haslohaslo" });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task B_coach_sees_all_players_with_parents_and_private_notes()
    {
        var players = await s.Coach.GetFromJsonAsync<List<PlayerSummary>>("/api/players");
        Assert.Equal(3, players!.Count);
        Assert.NotNull(players[0].Parents);

        var zosia = await s.Coach.GetFromJsonAsync<PlayerDetail>($"/api/players/{s.Zosia}");
        Assert.Equal("SEKRET-TRENERA", zosia!.PrivateNotes);
        Assert.Equal(5.0, zosia.Assessments.Single().Average);
    }

    [Fact]
    public async Task C_parent_sees_only_own_children()
    {
        var players = await s.Anna.GetFromJsonAsync<List<PlayerSummary>>("/api/players");
        Assert.Equal(["Kuba", "Zosia"], players!.Select(p => p.FirstName).Order());
        Assert.All(players!, p => Assert.Null(p.Parents));

        var piotrs = await s.Piotr.GetFromJsonAsync<List<PlayerSummary>>("/api/players");
        Assert.Equal("Maja", Assert.Single(piotrs!).FirstName);
    }

    [Fact]
    public async Task D_parent_gets_own_child_without_private_data()
    {
        var zosia = await s.Anna.GetFromJsonAsync<PlayerDetail>($"/api/players/{s.Zosia}");
        Assert.Null(zosia!.PrivateNotes);
        Assert.Null(zosia.Parents);
        Assert.Single(zosia.Assessments);
    }

    [Fact]
    public async Task E_parent_cannot_open_another_familys_child()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await s.Anna.GetAsync($"/api/players/{s.Maja}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Piotr.GetAsync($"/api/players/{s.Zosia}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Anna.GetAsync("/api/players/99999")).StatusCode);
    }

    [Fact]
    public async Task F_parent_cannot_use_coach_actions()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Anna.GetAsync("/api/parents")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Anna.PostAsJsonAsync("/api/players",
            new PlayerForm { FirstName = "X", LastName = "Y" })).StatusCode);
        var a = new AssessmentForm();
        a.Scores["forehand"] = 10;
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Anna.PostAsJsonAsync($"/api/players/{s.Zosia}/assessments", a)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Anna.DeleteAsync($"/api/players/{s.Maja}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Anna.PutAsync($"/api/players/{s.Maja}/parents/{s.AnnaId}", null)).StatusCode);
    }

    [Fact]
    public async Task G_anonymous_and_bad_credentials_are_rejected()
    {
        var anon = s.NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/players")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = "anna@test.pl", Password = "zlehaslo" })).StatusCode);
    }

    [Fact]
    public async Task H_invalid_input_is_rejected_with_validation_errors()
    {
        var res = await s.Coach.PostAsJsonAsync("/api/players", new PlayerForm { FirstName = "", LastName = "" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var bad = new AssessmentForm();
        bad.Scores["forehand"] = 11;
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Coach.PostAsJsonAsync($"/api/players/{s.Zosia}/assessments", bad)).StatusCode);
    }

    [Fact]
    public async Task I_api_responses_are_not_cached()
    {
        var res = await s.Anna.GetAsync("/api/players");
        Assert.Equal("no-store", res.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Z_password_reset_logs_the_parent_out()
    {
        var res = await s.Coach.PostAsync($"/api/parents/{s.AnnaId}/reset-password", null);
        var creds = await res.EnsureSuccessStatusCode().Content.ReadFromJsonAsync<CreatedCredentials>();
        Assert.Equal(HttpStatusCode.Unauthorized, (await s.Anna.GetAsync("/api/players")).StatusCode);

        var anna = await s.Login("anna@test.pl", creds!.TemporaryPassword!);
        Assert.Equal(2, (await anna.GetFromJsonAsync<List<PlayerSummary>>("/api/players"))!.Count);
    }
}
