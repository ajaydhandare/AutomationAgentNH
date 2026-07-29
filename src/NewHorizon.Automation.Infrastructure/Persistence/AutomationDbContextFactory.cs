using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace NewHorizon.Automation.Infrastructure.Persistence;

/// <summary>
/// Used only by <c>dotnet ef</c> at design time. It reads the same bootstrap file the service
/// reads, so migrations are never generated against a different schema than the one that runs.
/// </summary>
public sealed class AutomationDbContextFactory : IDesignTimeDbContextFactory<AutomationDbContext>
{
    private const string FallbackConnectionString =
        "Server=.;Database=NewHorizon_Automation;Trusted_Connection=True;TrustServerCertificate=True;";

    public AutomationDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString =
            configuration["AutomationAgent:Database:ConnectionString"] ?? FallbackConnectionString;

        var options = new DbContextOptionsBuilder<AutomationDbContext>()
            .UseSqlServer(
                connectionString,
                sql => sql.MigrationsAssembly(typeof(AutomationDbContext).Assembly.FullName))
            .Options;

        return new AutomationDbContext(options);
    }
}
