using Microsoft.Extensions.Options;
using NewHorizon.Automation.Application.Configuration;

namespace NewHorizon.Automation.Worker.Configuration;

/// <summary>
/// Binds the single bootstrap section and validates it at startup, so a misconfigured server
/// fails immediately and visibly rather than at the first ERP call.
/// </summary>
public static class OptionsRegistration
{
    public static IServiceCollection AddAutomationAgentOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IValidateOptions<AutomationAgentOptions>, AutomationAgentOptionsValidator>();

        services.AddOptions<AutomationAgentOptions>()
            .Bind(configuration.GetSection(AutomationAgentOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Database.ConnectionString),
                "AutomationAgent:Database:ConnectionString must be set.")
            .Validate(
                options => Uri.TryCreate(options.ErpApi.BaseUrl, UriKind.Absolute, out _),
                "AutomationAgent:ErpApi:BaseUrl must be an absolute URL.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Host.InboundApiKey),
                "AutomationAgent:Host:InboundApiKey must be set; the agent API is never left unauthenticated.")
            .ValidateOnStart();

        services.AddSingleton(sp => sp.GetRequiredService<IOptions<AutomationAgentOptions>>().Value);

        return services;
    }
}
