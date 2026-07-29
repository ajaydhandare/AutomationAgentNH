namespace NewHorizon.Automation.ErpClient.Authentication;

/// <summary>
/// Supplies the ERP service token. Operation code never calls this — the auth handler does — so
/// no workflow ever deals with token lifetime.
/// </summary>
public interface IErpTokenProvider
{
    /// <summary>Returns a cached token, acquiring or refreshing one only when necessary.</summary>
    Task<string> GetTokenAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Discards the cached token so the next call re-authenticates. Used when the ERP rejects a
    /// token the agent believed was still valid — after a server restart that invalidated
    /// signing keys, for instance.
    /// </summary>
    void Invalidate();
}
