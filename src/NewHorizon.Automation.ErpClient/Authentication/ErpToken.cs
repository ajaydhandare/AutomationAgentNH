namespace NewHorizon.Automation.ErpClient.Authentication;

/// <summary>
/// A cached ERP login token and the moment it stops being usable.
/// </summary>
public sealed record ErpToken(string AccessToken, DateTimeOffset ExpiresAtUtc)
{
    /// <summary>
    /// How far ahead of real expiry the token is treated as stale. A token that expires while
    /// a request is in flight would surface as a spurious 401 mid-operation, so it is replaced
    /// before it can happen.
    /// </summary>
    public static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(2);

    public bool IsUsableAt(DateTimeOffset nowUtc) => nowUtc < ExpiresAtUtc - RefreshMargin;
}
