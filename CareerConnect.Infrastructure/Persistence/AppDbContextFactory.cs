using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pgvector.EntityFrameworkCore;

namespace CareerConnect.Infrastructure.Persistence;

/// <summary>
/// Builds the EF model for migration commands without starting the API.
/// A migration command uses a local placeholder unless a database connection is explicitly supplied.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var startupAssembly = Assembly.Load("CareerConnect.Api");
        var startupDirectory = Path.GetDirectoryName(startupAssembly.Location)
            ?? Directory.GetCurrentDirectory();
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(startupDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddUserSecrets(startupAssembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = Environment.GetEnvironmentVariable("CAREERCONNECT_MIGRATION_CONNECTION")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=CareerConnectDesignTime;Username=design;Password=design";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                connectionString,
                provider => provider.UseVector())
            .Options;

        return new AppDbContext(options);
    }
}
