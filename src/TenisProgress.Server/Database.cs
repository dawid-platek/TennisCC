using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TenisProgress.Server.Data;

namespace TenisProgress.Server;

public static class Database
{
    /// <summary>Creates the database folder and applies pending EF Core migrations.</summary>
    public static void Prepare(IServiceProvider services, string connectionString)
    {
        var file = new SqliteConnectionStringBuilder(connectionString).DataSource;
        var dir = Path.GetDirectoryName(Path.GetFullPath(file));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
    }
}
