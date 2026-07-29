using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NewHorizon.Automation.Application.Abstractions;
using NewHorizon.Automation.Application.Configuration;
using NewHorizon.Automation.Application.Jobs;
using NewHorizon.Automation.Infrastructure.Persistence;
using NewHorizon.Automation.Infrastructure.Time;

namespace NewHorizon.Automation.Infrastructure;

/// <summary>
/// Wires the automation database and the adapters that implement the Application ports.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddAutomationInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<AutomationDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(AutomationDbContext).Assembly.FullName);

                // Transient SQL faults are the database's own business; workflow-level retry
                // handles anything that survives this.
                sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
            }));

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<IAutomationConfigRepository, AutomationConfigRepository>();

        services.AddHealthChecks()
            .AddCheck<AutomationDatabaseHealthCheck>("database", tags: ["ready"]);

        return services;
    }
}
