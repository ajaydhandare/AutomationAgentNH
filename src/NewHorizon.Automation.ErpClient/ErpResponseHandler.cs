using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NewHorizon.Automation.Application.Erp;

namespace NewHorizon.Automation.ErpClient;

/// <summary>
/// Turns an HTTP response into either a result or the correctly classified exception. This is the
/// one place where "retry" versus "ask a human" is decided, so the rule lives here rather than
/// being re-implemented per operation.
/// </summary>
internal static class ErpResponseHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static async Task<T> ReadAsync<T>(
        HttpResponseMessage response,
        string endpoint,
        CancellationToken cancellationToken)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(response);

        await EnsureSuccessAsync(response, endpoint, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<T>(SerializerOptions, cancellationToken);

        // A 200 with an unreadable body is a broken contract, not a business refusal; treating it
        // as transient lets a deploy that is mid-rollout recover on its own.
        return payload ?? throw new ErpTransientException(
            "The ERP returned an empty response. Automation will retry.",
            $"'{endpoint}' returned success with a body that could not be read as {typeof(T).Name}.",
            endpoint);
    }

    public static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string endpoint,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var error = await TryReadErrorAsync(response, cancellationToken);
        var status = (int)response.StatusCode;

        // 5xx, 408 and 429: the ERP is unwell or overloaded, and a later attempt may succeed.
        if (status >= 500 || response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests)
        {
            throw new ErpTransientException(
                "The ERP is temporarily unavailable. Automation will retry automatically.",
                $"'{endpoint}' returned {status}. {error?.BestTechnicalMessage}".TrimEnd(),
                endpoint);
        }

        // 401 reaching here means the auth handler already re-authenticated and was refused
        // again — the service account genuinely lacks access, which no amount of retrying fixes.
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new ErpBusinessException(
                "Automation is not permitted to perform this action in the ERP. Check the AUTOMATION_AGENT role.",
                $"'{endpoint}' returned {status} after re-authentication. {error?.BestTechnicalMessage}".TrimEnd(),
                endpoint);
        }

        // Everything else in 4xx is the ERP understanding the request and refusing it.
        throw new ErpBusinessException(
            error?.BestLaymanMessage ?? "The ERP rejected this request. Please review the document and try again.",
            $"'{endpoint}' returned {status}. {error?.BestTechnicalMessage}".TrimEnd(),
            endpoint);
    }

    private static async Task<ErpErrorResponse?> TryReadErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ErpErrorResponse>(SerializerOptions, cancellationToken);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or HttpRequestException)
        {
            // A non-JSON error body (an IIS HTML error page, typically) must not mask the real
            // status code, which is the part that actually drives the retry decision.
            return null;
        }
    }
}
