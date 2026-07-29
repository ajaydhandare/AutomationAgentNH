using Microsoft.Extensions.DependencyInjection;
using NewHorizon.Automation.Application.Workflows;
using NewHorizon.Automation.Application.Workflows.Definitions;

namespace NewHorizon.Automation.Application;

/// <summary>
/// Registers the engine and the workflow catalog.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddAutomationApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Adding a workflow is one line here plus one definition file. Everything downstream —
        // queue, engine, retry, logging, API — is unchanged by it.
        services.AddSingleton<IWorkflowCatalog>(_ => new WorkflowCatalog(
        [
            SjoWorkflow.Create(),
            OafWorkflow.Create(),
            MilWorkflow.Create(),
            CbomWorkflow.Create(),
            AutoShopWorkflow.Create(),
            AutoShopCycleWorkflow.Create(),
        ]));

        services.AddScoped<IWorkflowEngine, WorkflowEngine>();

        return services;
    }
}
