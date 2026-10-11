using Crm.BuildingBlocks.Presentation.Localization;
using Crm.BuildingBlocks.Tests.Support;
using Crm.BuildingBlocks.Tests.Support.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.BuildingBlocks.Tests.Presentation.Localization;

public sealed class RequestLocalizationExtensionsTests
{
    private sealed record Observed(string UiCulture, string Culture);

    private static async Task<(Observed Observed, RecordingResponseFeature Feature)> RunAsync(string? acceptLanguage)
    {
        using var restore = UiCulture.Use("en");
        var services = new ServiceCollection();
        services.AddLogging();
        var app = new ApplicationBuilder(services.BuildServiceProvider());
        app.UseCrmRequestLocalization();

        Observed? observed = null;
        app.Run(_ =>
        {
            observed = new Observed(
                System.Globalization.CultureInfo.CurrentUICulture.Name, System.Globalization.CultureInfo.CurrentCulture.Name);
            return Task.CompletedTask;
        });

        var feature = new RecordingResponseFeature();
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(feature);
        context.Features.Set<IHttpResponseBodyFeature>(feature);
        if (acceptLanguage is not null)
        {
            context.Request.Headers.AcceptLanguage = acceptLanguage;
        }

        await app.Build()(context);
        await feature.RunStartingAsync();

        return (Assert.IsType<Observed>(observed), feature);
    }

    [Fact]
    public async Task ArabicSetsBothCulturesToAr()
    {
        var (observed, _) = await RunAsync("ar");

        Assert.Equal("ar", observed.UiCulture);
        Assert.Equal("ar", observed.Culture);
    }

    [Fact]
    public async Task EnglishSetsBothCulturesToEn()
    {
        var (observed, _) = await RunAsync("en");

        Assert.Equal("en", observed.UiCulture);
        Assert.Equal("en", observed.Culture);
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("de-DE")]
    [InlineData("zh")]
    public async Task AnUnsupportedLanguageFallsBackToTheEnglishDefault(string header)
    {
        var (observed, _) = await RunAsync(header);

        Assert.Equal("en", observed.UiCulture);
    }

    [Fact]
    public async Task AnAbsentHeaderUsesTheEnglishDefault()
    {
        var (observed, _) = await RunAsync(null);

        Assert.Equal("en", observed.UiCulture);
        Assert.Equal("en", observed.Culture);
    }

    [Fact]
    public async Task ArabicSaudiArabiaIsObservedToFallBackToNeutralArabic()
    {
        // R9: pinned from observation. The neutral "ar" keeps .NET off the Um Al-Qura calendar (see CrmCultures).
        var (observed, _) = await RunAsync("ar-SA");

        Assert.Equal("ar", observed.UiCulture);
        Assert.Equal("ar", observed.Culture);
    }

    [Fact]
    public async Task TheQualityOrderOfTheHeaderIsHonoured()
    {
        var (observed, _) = await RunAsync("fr;q=0.9, ar;q=0.8, en;q=0.1");

        Assert.Equal("ar", observed.UiCulture);
    }

    [Theory]
    [InlineData("ar", "ar")]
    [InlineData("en", "en")]
    [InlineData("fr", "en")]
    public async Task TheContentLanguageHeaderIsSetOnTheResponse(string header, string expected)
    {
        var (_, feature) = await RunAsync(header);

        Assert.Equal(expected, feature.Headers.ContentLanguage.ToString());
    }
}
