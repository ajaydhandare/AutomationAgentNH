using NewHorizon.Automation.Application;
using NewHorizon.Automation.Application.Configuration;
using NewHorizon.Automation.ErpClient;
using NewHorizon.Automation.Infrastructure;
using NewHorizon.Automation.Worker.Configuration;
using NewHorizon.Automation.Worker.Diagnostics;
using NewHorizon.Automation.Worker.Endpoints;
using NewHorizon.Automation.Worker.Logging;
using Serilog;

// Bootstrap logger: captures failures that happen before the host (and its configured logger) exists.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    // ContentRootPath is pinned to the binary location: as a Windows Service the working directory
    // is %WINDIR%\System32, which would otherwise hide appsettings.json.
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory,
    });

    builder.Host.UseWindowsService(options => options.ServiceName = "NewHorizon Automation Agent");

    builder.Host.UseSerilog((context, _, loggerConfiguration) =>
        SerilogConfigurator.Configure(loggerConfiguration, context.Configuration));

    builder.Services.AddAutomationAgentOptions(builder.Configuration);
    builder.Services.AddSingleton(new AgentRuntimeInfo(DateTimeOffset.UtcNow));
    builder.Services.AddProblemDetails();

    var connectionString =
        builder.Configuration[
            $"{AutomationAgentOptions.SectionName}:{nameof(AutomationAgentOptions.Database)}:{nameof(DatabaseOptions.ConnectionString)}"]
        ?? throw new InvalidOperationException(
            "AutomationAgent:Database:ConnectionString is required to start the agent.");

    builder.Services.AddAutomationApplication();
    builder.Services.AddAutomationInfrastructure(connectionString);
    builder.Services.AddErpClient();

    // The management/read API is for the ERP only. Loopback binding is the outer boundary;
    // the inbound API key (Phase 6) is the inner one.
    var hostOptions = builder.Configuration
        .GetSection($"{AutomationAgentOptions.SectionName}:{nameof(AutomationAgentOptions.Host)}")
        .Get<AgentHostOptions>() ?? new AgentHostOptions();

    builder.WebHost.ConfigureKestrel(kestrel =>
    {
        if (hostOptions.BindToLoopbackOnly)
        {
            kestrel.ListenLocalhost(hostOptions.ManagementApiPort);
        }
        else
        {
            kestrel.ListenAnyIP(hostOptions.ManagementApiPort);
        }

        kestrel.AddServerHeader = false;
    });

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseExceptionHandler();

    app.MapHealthEndpoints();

    Log.Information(
        "Automation agent starting on port {Port} (loopbackOnly={LoopbackOnly})",
        hostOptions.ManagementApiPort,
        hostOptions.BindToLoopbackOnly);

    await app.RunAsync();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Automation agent terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>
/// Exposed so the integration tests can host the agent through <c>WebApplicationFactory</c>.
/// </summary>
public partial class Program;
