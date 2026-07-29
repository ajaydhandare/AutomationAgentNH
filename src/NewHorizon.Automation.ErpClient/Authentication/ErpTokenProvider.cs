using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewHorizon.Automation.Application.Abstractions;
using NewHorizon.Automation.Application.Configuration;
using NewHorizon.Automation.Application.Erp;

namespace NewHorizon.Automation.ErpClient.Authentication;

/// <summary>
/// Signs in to the ERP and caches the token it issues. Registered as a singleton: the whole process
/// shares one token, so N parallel workers cause one login, not N.
/// </summary>
/// <remarks>
/// Nothing outside this class ever logs in. Operations call the ERP through
/// <see cref="ErpAuthHandler"/>, which asks for the token here — so a first call, an expiry, and a
/// rejected token all resolve to the same single path, and an operation body never sees any of them.
/// </remarks>
public sealed class ErpTokenProvider : IErpTokenProvider, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ErpApiOptions _options;
    private readonly IClock _clock;
    private readonly ILogger<ErpTokenProvider> _logger;

    // Guards the acquisition, not the read: a stampede of workers arriving at expiry must produce
    // exactly one call to the ERP, with the rest using the token it fetched.
    private readonly SemaphoreSlim _acquisitionLock = new(1, 1);

    private volatile ErpToken? _cachedToken;

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
        // Fast path: no lock while the cached token is comfortably valid, which is almost always —
        // the ERP issues 24-hour tokens, so a whole day of cycles runs off one login.
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
                "ERP login successful for user {UserName} at {LoggedInAtUtc:O}; token cached until {ExpiresAtUtc:O}",
                _options.UserName,
                _clock.UtcNow,
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
        _logger.LogWarning("ERP token invalidated; the next call will sign in again");
    }

    private async Task<ErpToken> AcquireAsync(CancellationToken cancellationToken)
    {
        var endpoint = _options.LoginPath;

        var request = new ErpLoginRequest
        {
            UserName = _options.UserName,
            Password = _options.Password,
            ConnectionString = _options.LoginConnectionString,
            IsCeFlag = _options.IsCeFlag,
            AppId = _options.AppId,
            UserId = _options.UserId,
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
                $"Login request to '{endpoint}' failed: {ex.Message}",
                endpoint,
                ex);
        }

        using (response)
        {
            // The ERP answers a refusal with 400 and success=false, and a success with 200 and a
            // token, so the body decides the outcome and the status only colours the message.
            var payload = await ReadPayloadAsync(response, endpoint, cancellationToken);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                || (payload is { Success: false } && response.StatusCode is HttpStatusCode.BadRequest))
            {
                // Retrying will not fix a wrong password, so say so plainly rather than let this
                // disappear into a backoff loop.
                throw new ErpAuthenticationException(
                    "Automation could not sign in to the ERP. Check the agent's ERP credentials.",
                    $"Login endpoint '{endpoint}' rejected user '{_options.UserName}' with " +
                    $"{(int)response.StatusCode}: {payload?.Reason ?? "no reason given"}.",
                    endpoint);
            }

            if (!response.IsSuccessStatusCode || payload is not { Success: true })
            {
                throw new ErpAuthenticationException(
                    "The ERP could not sign the automation agent in. Automation will retry shortly.",
                    $"Login endpoint '{endpoint}' returned {(int)response.StatusCode}: " +
                    $"{payload?.Reason ?? "no reason given"}.",
                    endpoint);
            }

            if (payload.Data?.Token?.Value is not { Length: > 0 } accessToken)
            {
                throw new ErpAuthenticationException(
                    "The ERP returned an unusable sign-in token.",
                    $"Login endpoint '{endpoint}' reported success but carried no data.token.value.",
                    endpoint);
            }

            // Trust the ERP's own expiry when it states one; the configured TTL is only a fallback
            // for a response that omits it. A validTo already in the past would cache a dead token,
            // so it is treated as absent.
            var statedExpiry = payload.Data.Token.ValidTo?.ToUniversalTime();

            var expiresAtUtc = statedExpiry > _clock.UtcNow
                ? statedExpiry.Value
                : _clock.UtcNow + TimeSpan.FromHours(_options.TokenTtlHours);

            return new ErpToken(accessToken, expiresAtUtc);
        }
    }

    private async Task<ErpLoginResponse?> ReadPayloadAsync(
        HttpResponseMessage response,
        string endpoint,
        CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ErpLoginResponse>(cancellationToken);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            // A gateway error page rather than the ERP's envelope. Not fatal on its own — the
            // status checks below still classify it — so record it and carry on.
            _logger.LogDebug(
                ex,
                "Login endpoint {Endpoint} returned {StatusCode} with a body that is not the ERP envelope",
                endpoint,
                (int)response.StatusCode);

            return null;
        }
    }

    public void Dispose() => _acquisitionLock.Dispose();
}
