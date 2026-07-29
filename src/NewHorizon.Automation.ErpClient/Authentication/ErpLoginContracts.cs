using System.Text.Json.Serialization;

namespace NewHorizon.Automation.ErpClient.Authentication;

/// <summary>
/// Body posted to the ERP login endpoint. The property names are the ERP's, not ours — it is the
/// same contract the ERP UI posts, so the casing (<c>appID</c>, <c>isCEFlag</c>) is copied exactly.
/// </summary>
public sealed record ErpLoginRequest
{
    [JsonPropertyName("userName")]
    public required string UserName { get; init; }

    [JsonPropertyName("password")]
    public required string Password { get; init; }

    /// <summary>The ERP database the login resolves against.</summary>
    [JsonPropertyName("connStr")]
    public required string ConnectionString { get; init; }

    [JsonPropertyName("isCEFlag")]
    public bool IsCeFlag { get; init; }

    [JsonPropertyName("appID")]
    public string AppId { get; init; } = string.Empty;

    [JsonPropertyName("userId")]
    public string UserId { get; init; } = string.Empty;
}

/// <summary>
/// The ERP's standard response envelope. A refusal arrives as HTTP 400 with
/// <c>success = false</c> and a message key, so the status code alone never decides the outcome.
/// </summary>
public sealed record ErpLoginResponse
{
    [JsonPropertyName("data")]
    public ErpLoginData? Data { get; init; }

    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }

    /// <summary>Whichever explanation the ERP filled in, for the technical log line.</summary>
    public string? Reason => Message ?? ErrorMessage;
}

public sealed record ErpLoginData
{
    [JsonPropertyName("token")]
    public ErpLoginToken? Token { get; init; }

    [JsonPropertyName("uid")]
    public string? Uid { get; init; }
}

public sealed record ErpLoginToken
{
    /// <summary>The bearer token itself.</summary>
    [JsonPropertyName("value")]
    public string? Value { get; init; }

    /// <summary>
    /// Absolute expiry stated by the ERP (currently issue time + 24 hours). Preferred over any
    /// configured lifetime: only the ERP knows when it stops honouring the token.
    /// </summary>
    [JsonPropertyName("validTo")]
    public DateTimeOffset? ValidTo { get; init; }
}
