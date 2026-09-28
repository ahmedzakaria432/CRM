using Crm.BuildingBlocks.Infrastructure.Correlation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace Crm.BuildingBlocks.Infrastructure.Http;

public static class HttpClientBuilderExtensions
{
    /// <summary>
    /// Every outbound HTTP client (ERP, WhatsApp, email providers, webhooks) goes through this - never a
    /// hand-rolled retry loop. Correlation id first (outermost), then the standard resilience pipeline: total
    /// timeout -> retry -> circuit breaker -> per-attempt timeout, bound from "Resilience:Outbound". The breaker
    /// is ratio-based; with every attempt failing it opens after CircuitBreaker:MinimumThroughput attempts, and
    /// retries count as attempts. One circuit per named client.
    /// </summary>
    public static IHttpClientBuilder AddCrmResilience(this IHttpClientBuilder builder, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        builder.AddHttpMessageHandler<CorrelationIdDelegatingHandler>();
        builder.AddStandardResilienceHandler().Configure(configuration.GetSection("Resilience:Outbound"));

        return builder;
    }
}
