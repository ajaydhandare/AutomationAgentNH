using System.Text.Json.Serialization;

namespace NewHorizon.Automation.ErpClient.Authentication;

/// <summary>
/// Body returned by the ERP service-token endpoint. Field names follow the OAuth
/// client-credentials convention so the ERP team can implement it against a familiar shape.
/// </summary>
public sealed record ServiceTokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; init; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; init; }

    /// <summary>Lifetime in seconds. Absent or non-positive ⇒ the configured TTL is used.</summary>
    [JsonPropertyName("expires_in")]
    public int? ExpiresIn { get; init; }
}

/// <summary>Credentials posted to the ERP service-token endpoint.</summary>
public sealed record ServiceTokenRequest
{
    [JsonPropertyName("client_id")]
    public required string ClientId { get; init; }

    [JsonPropertyName("client_secret")]
    public required string ClientSecret { get; init; }

    [JsonPropertyName("grant_type")]
    public string GrantType { get; init; } = "client_credentials";
}
