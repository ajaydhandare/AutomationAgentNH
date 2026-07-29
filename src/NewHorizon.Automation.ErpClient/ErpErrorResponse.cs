using System.Text.Json.Serialization;

namespace NewHorizon.Automation.ErpClient;

/// <summary>
/// Error body the ERP returns on a rejected request. Every field is optional — the agent must
/// still produce a usable layman message when the ERP sends nothing but a status code.
/// </summary>
public sealed record ErpErrorResponse
{
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>Business-friendly text, when the ERP supplies one.</summary>
    [JsonPropertyName("userMessage")]
    public string? UserMessage { get; init; }

    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; init; }

    [JsonPropertyName("detail")]
    public string? Detail { get; init; }

    public string? BestLaymanMessage =>
        FirstNonBlank(UserMessage, Message);

    public string? BestTechnicalMessage =>
        FirstNonBlank(Detail, Message, UserMessage);

    private static string? FirstNonBlank(params string?[] candidates) =>
        candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));
}
