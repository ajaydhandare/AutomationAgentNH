namespace NewHorizon.Automation.IntegrationTests;

/// <summary>
/// Minimum bootstrap configuration required for the host to start.
/// </summary>
public static class TestConfiguration
{
    public static IReadOnlyDictionary<string, string?> Valid { get; } = new Dictionary<string, string?>
    {
        ["AutomationAgent:Database:ConnectionString"] =
            "Server=(localdb)\\MSSQLLocalDB;Database=NewHorizon_Automation_Tests;Trusted_Connection=True;TrustServerCertificate=True;",
        ["AutomationAgent:ErpApi:BaseUrl"] = "http://localhost/NH_API_TEST",
        ["AutomationAgent:ErpApi:ServiceTokenPath"] = "/api/auth/service-token",
        ["AutomationAgent:ErpApi:ClientId"] = "automation-agent",
        ["AutomationAgent:ErpApi:ClientSecret"] = "test-secret",
        ["AutomationAgent:ErpApi:TokenTtlMinutes"] = "60",
        ["AutomationAgent:ErpApi:TimeoutSeconds"] = "30",
        ["AutomationAgent:Host:ManagementApiPort"] = "5080",
        ["AutomationAgent:Host:BindToLoopbackOnly"] = "true",
        ["AutomationAgent:Host:InboundApiKey"] = "test-inbound-key",
        ["AutomationAgent:Defaults:PollIntervalSeconds"] = "30",
        ["AutomationAgent:Defaults:ReconciliationIntervalMinutes"] = "5",
        ["AutomationAgent:Defaults:ParallelWorkers"] = "4",
        ["AutomationAgent:Defaults:MaxRetry"] = "3",
        ["AutomationAgent:Serilog:MinimumLevel"] = "Warning",
    };
}
