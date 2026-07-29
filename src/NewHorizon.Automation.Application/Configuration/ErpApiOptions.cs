using System.ComponentModel.DataAnnotations;

namespace NewHorizon.Automation.Application.Configuration;

/// <summary>
/// Where the ERP lives and how the agent authenticates to it as a service identity.
/// <see cref="ClientSecret"/> is expected to come from a protected store in production
/// (DPAPI / machine store / environment), never from source control.
/// </summary>
public sealed class ErpApiOptions
{
    [Required(AllowEmptyStrings = false)]
    [Url]
    public string BaseUrl { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string ServiceTokenPath { get; init; } = "/api/auth/service-token";

    [Required(AllowEmptyStrings = false)]
    public string ClientId { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string ClientSecret { get; init; } = string.Empty;

    [Range(1, 1440)]
    public int TokenTtlMinutes { get; init; } = 60;

    [Range(1, 600)]
    public int TimeoutSeconds { get; init; } = 30;
}
