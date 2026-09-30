using System.Globalization;
using System.Threading.RateLimiting;
using Crm.BuildingBlocks.Application.Localization;
using Crm.BuildingBlocks.Presentation.ErrorHandling;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.BuildingBlocks.Presentation.RateLimiting;

/// <summary>Bound from "RateLimiting:Public".</summary>
public sealed record PublicRateLimitSettings(int PermitLimit, TimeSpan Window);

/// <summary>Named policies applied with [EnableRateLimiting] (Story 07).</summary>
public static class CrmRateLimitPolicies
{
    public const string SignIn = "sign-in";
}

public static class RateLimitingServiceCollectionExtensions
{
    /// <summary>
    /// Rate-limits every controller in the Public Area per client IP, with 429 + Retry-After and a
    /// <see cref="ProblemTypes.RateLimited"/> problem. The global limiter reads the endpoint's [Area] metadata
    /// (verified by probe while planning Story 03), so a Public controller in any module is covered with no
    /// attribute to forget. Other Areas are not limited here.
    /// </summary>
    public static IServiceCollection AddCrmRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        PublicRateLimitSettings settings = configuration.GetSection("RateLimiting:Public").Get<PublicRateLimitSettings>()
            ?? throw new InvalidOperationException("RateLimiting:Public is not configured.");

        PublicRateLimitSettings signIn = configuration.GetSection("RateLimiting:SignIn").Get<PublicRateLimitSettings>()
            ?? throw new InvalidOperationException("RateLimiting:SignIn is not configured.");

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Behind a reverse proxy RemoteIpAddress is the proxy's; UseForwardedHeaders must come first then.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                context.GetEndpoint()?.Metadata.GetMetadata<AreaAttribute>()?.RouteValue == CrmAreas.Public
                    ? RateLimitPartition.GetFixedWindowLimiter(
                        $"public:{context.Connection.RemoteIpAddress}",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = settings.PermitLimit,
                            Window = settings.Window,
                            QueueLimit = 0,
                        })
                    : RateLimitPartition.GetNoLimiter("unlimited"));
            options.AddPolicy(CrmRateLimitPolicies.SignIn, context => RateLimitPartition.GetFixedWindowLimiter(
                $"sign-in:{context.Connection.RemoteIpAddress}",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = signIn.PermitLimit, Window = signIn.Window, QueueLimit = 0 }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                ILocalizationService localization = context.HttpContext.RequestServices.GetRequiredService<ILocalizationService>();
                await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(
                    new ProblemDetailsContext
                    {
                        HttpContext = context.HttpContext,
                        ProblemDetails = new ProblemDetails
                        {
                            Status = StatusCodes.Status429TooManyRequests,
                            Type = ProblemTypes.RateLimited,
                            Title = ProblemTypes.TitleFor(ProblemTypes.RateLimited, localization),
                            Detail = localization.Get(CrmMessageKeys.ProblemDetail.RateLimited),
                        },
                    });
            };
        });

        return services;
    }
}
