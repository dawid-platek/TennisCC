using Microsoft.EntityFrameworkCore;
using TenisProgress.Server;
using TenisProgress.Server.Auth;
using TenisProgress.Server.Data;
using TenisProgress.Server.Endpoints;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=data/tenis.db";
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(connectionString));
builder.Services.AddAppAuth();
builder.Services.AddProblemDetails();
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(o =>
{
    // Behind a reverse proxy (HTTPS termination) trust X-Forwarded-Proto/For.
    o.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                         Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

Database.Prepare(app.Services, connectionString);

// `dotnet run -- seed` / `dotnet run -- create-coach ...` run a command and exit.
if (args.Length > 0 && await Commands.TryRunAsync(args, app.Services)) return;

app.UseForwardedHeaders();
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler();
    app.UseHsts();
}

app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h.XContentTypeOptions = "nosniff";
    h.XFrameOptions = "DENY";
    h["Referrer-Policy"] = "same-origin";
    h.ContentSecurityPolicy =
        "default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; connect-src 'self'; worker-src 'self'; manifest-src 'self'; " +
        "base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
    // API responses contain children's data: never keep them in any cache.
    if (ctx.Request.Path.StartsWithSegments("/api")) h.CacheControl = "no-store";
    await next();
});

// Serves the Blazor WebAssembly client (fingerprinted, compressed static assets).
app.MapStaticAssets();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapPlayerEndpoints();
app.MapParentEndpoints();
app.Map("/api/{**rest}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
