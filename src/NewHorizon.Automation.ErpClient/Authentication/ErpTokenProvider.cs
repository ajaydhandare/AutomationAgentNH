using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewHorizon.Automation.Application.Abstractions;
using NewHorizon.Automation.Application.Configuration;
using NewHorizon.Automation.Application.Erp;

namespace NewHorizon.Automation.ErpClient.Authentication;

/// <summary>
/// Acquires and caches the ERP service token. Registered as a singleton: the whole process shares
/// one token, so N parallel workers cause one authentication, not N.
/// </summary>
public sealed class ErpTokenProvider : IErpTokenProvider, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ErpApiOptions _options;
    private readonly IClock _clock;
    private readonly ILogger<ErpTokenProvider> _logger;

    // Guards the acquisition, not the read: a stampede of workers arriving at expiry must produce
    // exactly one call to the ERP, with the rest using the token it fetched.
    private readonly SemaphoreSlim _acquisitionLock = new(1, 1);

    private volatile ServiceToken? _cachedToken;

    public ErpTokenProvider(
        HttpClient httpClient,
        IOptions<AutomationAgentOptions> options,
        IClock clock,
        ILogger<ErpTokenProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _options = options.Value.ErpApi;
        _clock = clock;
        _logger = logger;
    }

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        // Fast path: no lock while the cached token is comfortably valid, which is almost always.
        if (_cachedToken is { } cached && cached.IsUsableAt(_clock.UtcNow))
        {
            return cached.AccessToken;
        }

        await _acquisitionLock.WaitAsync(cancellationToken);
        try
        {
            // Re-check inside the lock: another caller may have refreshed while this one waited.
            if (_cachedToken is { } current && current.IsUsableAt(_clock.UtcNow))
            {
                return current.AccessToken;
            }

            var token = await AcquireAsync(cancellationToken);
            _cachedToken = token;

            _logger.LogInformation(
                "Acquired ERP service token for client {ClientId}, valid until {ExpiresAtUtc:O}",
                _options.ClientId,
                token.ExpiresAtUtc);

            return token.AccessToken;
        }
        finally
        {
            _acquisitionLock.Release();
        }
    }

    public void Invalidate()
    {
        _cachedToken = null;
        _logger.LogWarning("ERP service token invalidated; the next call will re-authenticate");
    }

    private async Task<ServiceToken> AcquireAsync(CancellationToken cancellationToken)
    {
        var endpoint = _options.ServiceTokenPath;

        var request = new ServiceTokenRequest
        {
            ClientId = _options.ClientId,
            ClientSecret = _options.ClientSecret,
        };

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(endpoint, request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new ErpAuthenticationException(
                "Could not reach the ERP to sign in. Automation will retry shortly.",
                $"Service-token request to '{endpoint}' failed: {ex.Message}",
                endpoint,
                ex);
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                // Retrying will not fix a wrong secret, so say so plainly rather than let this
                // disappear into a backoff loop.
                throw new ErpAuthenticationException(
                    "Automation could not sign in to the ERP. Check the agent's service credentials.",
                    $"Service-token endpoint '{endpoint}' rejected client '{_options.ClientId}' with {(int)response.StatusCode}.",
                    endpoint);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new ErpAuthenticationException(
                    "The ERP could not issue a sign-in token. Automation will retry shortly.",
                    $"Service-token endpoint '{endpoint}' returned {(int)response.StatusCode}.",
                    endpoint);
            }

            var payload = await response.Content.ReadFromJsonAsync<ServiceTokenResponse>(cancellationToken);

            if (payload?.AccessToken is not { Length: > 0 } accessToken)
            {
                throw new ErpAuthenticationException(
                    "The ERP returned an unusable sign-in token.",
                    $"Service-token endpoint '{endpoint}' returned a body with no access_token.",
                    endpoint);
            }

            // Trust the ERP's own lifetime when it sends one; fall back to the configured TTL.
            var lifetime = payload.ExpiresIn is > 0
                ? TimeSpan.FromSeconds(payload.ExpiresIn.Value)
                : TimeSpan.FromMinutes(_options.TokenTtlMinutes);

            return new ServiceToken(accessToken, _clock.UtcNow + lifetime);
        }
    }

    public void Dispose() => _acquisitionLock.Dispose();
}
