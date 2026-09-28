using Crm.BuildingBlocks.Application.Abstractions;

namespace Crm.BuildingBlocks.Infrastructure.Correlation;

/// <summary>Forwards the current correlation id on every outbound call made through AddCrmResilience.</summary>
public sealed class CorrelationIdDelegatingHandler(ICorrelationIdAccessor correlation) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (correlation.CorrelationId is { } correlationId && !request.Headers.Contains(CorrelationIdMiddleware.HeaderName))
        {
            request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
