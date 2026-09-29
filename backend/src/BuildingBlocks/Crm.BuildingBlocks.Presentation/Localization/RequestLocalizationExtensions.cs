using Crm.BuildingBlocks.Application.Localization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;

namespace Crm.BuildingBlocks.Presentation.Localization;

public static class RequestLocalizationExtensions
{
    /// <summary>Accept-Language (en, ar) sets CurrentCulture and CurrentUICulture for the request. The only
    /// provider: no query string, no cookie - the frontend sends its UI locale as Accept-Language on every call.
    /// Must run BEFORE UseExceptionHandler and UseStatusCodePages, or their problem bodies stay English (Probe 1).</summary>
    public static IApplicationBuilder UseCrmRequestLocalization(this IApplicationBuilder app) =>
        app.UseRequestLocalization(options =>
        {
            string[] cultures = [.. CrmCultures.All];
            options.SetDefaultCulture(CrmCultures.Default)
                .AddSupportedCultures(cultures)
                .AddSupportedUICultures(cultures);
            options.RequestCultureProviders = [new AcceptLanguageHeaderRequestCultureProvider()];
            options.ApplyCurrentCultureToResponseHeaders = true; // Content-Language on every response
        });
}
