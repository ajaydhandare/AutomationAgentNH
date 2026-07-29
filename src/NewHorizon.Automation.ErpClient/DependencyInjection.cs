using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using NewHorizon.Automation.Application.Configuration;
using NewHorizon.Automation.Application.Erp;
using NewHorizon.Automation.ErpClient.Authentication;
using Polly;

namespace NewHorizon.Automation.ErpClient;

/// <summary>
/// Wires the ERP client, its resilience pipeline and its authentication.
/// </summary>
public static class DependencyInjection
{
    public const string ErpHttpClientName = "ErpApi";
    public const string TokenHttpClientName = "ErpAuth";

    public static IServiceCollection AddErpClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The token client is deliberately plain: no auth handler (it *is* the auth call, and
        // adding one would recurse) and no retry beyond its own timeout, because a bad secret
        // must fail fast and loudly rather than be retried into a backoff loop.
        services.AddHttpClient(TokenHttpClientName, ConfigureBaseAddress);

        services.AddSingleton<IErpTokenProvider>(serviceProvider =>
            ActivatorUtilities.CreateInstance<ErpTokenProvider>(
                serviceProvider,
                serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(TokenHttpClientName)));

        services.AddTransient<ErpAuthHandler>();

        var erpClientBuilder = services.AddHttpClient<IErpClient, HttpErpClient>(
            ErpHttpClientName,
            ConfigureBaseAddress);

        // Registration order is execution order, outermost first. Resilience wraps auth so a
        // retried attempt re-enters the auth handler and picks up a refreshed token; were it the
        // other way round, a retry after a long backoff could replay a token that has since expired.
        erpClientBuilder.AddResilienceHandler("erp", BuildPipeline);
        erpClientBuilder.AddHttpMessageHandler<ErpAuthHandler>();

        services.AddHealthChecks()
            .AddCheck<ErpApiHealthCheck>("erpApi", tags: ["ready"]);

        return services;
    }

    private static void ConfigureBaseAddress(IServiceProvider serviceProvider, HttpClient client)
    {
        var options = serviceProvider.GetRequiredService<IOptions<AutomationAgentOptions>>().Value.ErpApi;

        client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);

        // Timeout is enforced by the pipeline below; leaving HttpClient's own timeout infinite
        // stops it cancelling an attempt the pipeline still intends to manage.
        client.Timeout = Timeout.InfiniteTimeSpan;
    }

    private static void BuildPipeline(
        ResiliencePipelineBuilder<HttpResponseMessage> builder,
        ResilienceHandlerContext context)
    {
        var options = context.ServiceProvider
            .GetRequiredService<IOptions<AutomationAgentOptions>>().Value;

        var attemptTimeout = TimeSpan.FromSeconds(options.ErpApi.TimeoutSeconds);

        builder
            // Total budget across all attempts, so one stuck operation cannot hold a worker for
            // minutes while other jobs queue behind it.
            .AddTimeout(new Polly.Timeout.TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(Math.Max(60, options.ErpApi.TimeoutSeconds * 4)),
                Name = "erp-total-timeout",
            });

        // MaxRetry is legitimately configurable down to zero ("never retry at the transport
        // level"), but Polly rejects a retry strategy with no attempts, so the strategy is
        // omitted entirely rather than added with an invalid count.
        if (options.Defaults.MaxRetry > 0)
        {
            builder.AddRetry(new Polly.Retry.RetryStrategyOptions<HttpResponseMessage>
            {
                Name = "erp-retry",
                MaxRetryAttempts = options.Defaults.MaxRetry,
                BackoffType = DelayBackoffType.Exponential,
                // Jitter keeps N parallel workers from retrying in lockstep and hammering an ERP
                // that is already struggling.
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(1),
                ShouldHandle = static arguments => ValueTask.FromResult(IsTransient(arguments.Outcome)),
            });
        }

        builder
            .AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                Name = "erp-circuit-breaker",
                FailureRatio = 0.5,
                MinimumThroughput = 10,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(15),
                ShouldHandle = static arguments => ValueTask.FromResult(IsTransient(arguments.Outcome)),
            })
            // Per-attempt timeout, innermost, so a hung attempt is abandoned and retried rather
            // than consuming the whole budget.
            .AddTimeout(new Polly.Timeout.TimeoutStrategyOptions
            {
                Timeout = attemptTimeout,
                Name = "erp-attempt-timeout",
            });
    }

    /// <summary>
    /// Only transient conditions are retried or counted against the breaker. A business refusal
    /// (a 400 for a missing vendor) is deterministic: retrying produces the same answer and would
    /// wrongly trip the breaker against a perfectly healthy ERP.
    /// </summary>
    private static bool IsTransient(Outcome<HttpResponseMessage> outcome)
    {
        if (outcome.Exception is { } exception)
        {
            return exception is HttpRequestException or TaskCanceledException or TimeoutException;
        }

        if (outcome.Result is not { } response)
        {
            return false;
        }

        var status = (int)response.StatusCode;

        return status >= 500
            || response.StatusCode is System.Net.HttpStatusCode.RequestTimeout
            || response.StatusCode is System.Net.HttpStatusCode.TooManyRequests;
    }
}
