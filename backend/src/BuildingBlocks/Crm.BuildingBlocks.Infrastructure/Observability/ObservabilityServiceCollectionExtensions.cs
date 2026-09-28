using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Diagnostics;
using Crm.BuildingBlocks.Infrastructure.Correlation;
using Crm.BuildingBlocks.Infrastructure.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Core;
using Serilog.Formatting.Compact;
using Serilog.Sinks.OpenTelemetry;

namespace Crm.BuildingBlocks.Infrastructure.Observability;

public static class ObservabilityServiceCollectionExtensions
{
    /// <summary>
    /// Serilog (compact JSON on the console in every environment, OTLP when configured), the correlation-id
    /// accessor, and OpenTelemetry traces and metrics for HTTP in and out, EF Core, and MediatR. OTLP export is
    /// on only when <c>OpenTelemetry:OtlpEndpoint</c> is set.
    /// </summary>
    public static IServiceCollection AddCrmObservability(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddHttpContextAccessor();
        services.AddSingleton<ICorrelationIdAccessor, CorrelationIdAccessor>();
        services.AddSingleton<ILogEventEnricher, CrmLogEnricher>();

        string? otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"];

        services.AddSerilog((serviceProvider, logger) =>
        {
            logger.ReadFrom.Configuration(configuration) // MinimumLevel and overrides only
                .ReadFrom.Services(serviceProvider)       // CrmLogEnricher
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", environment.ApplicationName)
                .Destructure.With(new SensitiveDataDestructuringPolicy())
                .Enrich.With(new SensitivePropertyRedactionEnricher()) // after every other enricher
                .WriteTo.Console(new RenderedCompactJsonFormatter());  // JSON in every environment - not configurable

            if (otlpEndpoint is not null)
            {
                logger.WriteTo.OpenTelemetry(options =>
                {
                    options.Endpoint = otlpEndpoint;
                    options.Protocol = OtlpProtocol.Grpc;
                    options.ResourceAttributes["service.name"] = environment.ApplicationName;
                });
            }
        });

        OpenTelemetryBuilder openTelemetry = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(environment.ApplicationName))
            .WithTracing(tracing => tracing
                .AddSource(CrmDiagnostics.SourceName)
                .AddAspNetCoreInstrumentation(options =>
                    options.Filter = context => !context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation())
            .WithMetrics(metrics => metrics
                .AddMeter(CrmDiagnostics.SourceName)
                .AddMeter("Polly") // resilience events: retries, circuit state
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation());

        if (otlpEndpoint is not null)
        {
            openTelemetry.UseOtlpExporter(OtlpExportProtocol.Grpc, new Uri(otlpEndpoint));
        }

        return services;
    }

    /// <summary>One completion line per request (Serilog's default template - no query string).</summary>
    public static IApplicationBuilder UseCrmRequestLogging(this IApplicationBuilder app) =>
        app.UseSerilogRequestLogging(options =>
            options.EnrichDiagnosticContext = (diagnostics, http) => diagnostics.Set("RequestHost", http.Request.Host.Value));
}
