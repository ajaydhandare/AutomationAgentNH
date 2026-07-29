using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace NewHorizon.Automation.ErpClient.Authentication;

/// <summary>
/// Attaches the service token to every ERP call and recovers from a rejected one. Because this
/// sits in the pipeline, no operation body ever sees a token, an expiry, or a 401.
/// </summary>
public sealed class ErpAuthHandler : DelegatingHandler
{
    private readonly IErpTokenProvider _tokenProvider;
    private readonly ILogger<ErpAuthHandler> _logger;

    public ErpAuthHandler(IErpTokenProvider tokenProvider, ILogger<ErpAuthHandler> logger)
    {
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var token = await _tokenProvider.GetTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode is not HttpStatusCode.Unauthorized)
        {
            return response;
        }

        // The token was refused despite looking valid — the ERP restarted, or its signing key
        // rotated. Re-authenticate and replay exactly once: a second 401 is a real authorisation
        // problem (a missing AUTOMATION_AGENT role, say) that retrying would only hide.
        _logger.LogWarning(
            "ERP rejected the service token for {Method} {Uri}; re-authenticating and retrying once",
            request.Method,
            request.RequestUri);

        response.Dispose();
        _tokenProvider.Invalidate();

        var refreshedToken = await _tokenProvider.GetTokenAsync(cancellationToken);

        // A request message cannot be sent twice, so the retry needs a fresh copy.
        using var retryRequest = await CloneAsync(request, cancellationToken);
        retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshedToken);

        return await base.SendAsync(retryRequest, cancellationToken);
    }

    private static async Task<HttpRequestMessage> CloneAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy,
        };

        if (request.Content is not null)
        {
            // Buffer the body: the original content stream is already consumed by the first attempt.
            var buffer = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            var content = new ByteArrayContent(buffer);

            foreach (var header in request.Content.Headers)
            {
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            clone.Content = content;
        }

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var option in request.Options)
        {
            clone.Options.TryAdd(option.Key, option.Value);
        }

        return clone;
    }
}
