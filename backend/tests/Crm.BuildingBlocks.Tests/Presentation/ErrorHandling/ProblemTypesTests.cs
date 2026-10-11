using Crm.BuildingBlocks.Application.Localization;
using Crm.BuildingBlocks.Presentation.ErrorHandling;
using Crm.BuildingBlocks.Tests.Support;

namespace Crm.BuildingBlocks.Tests.Presentation.ErrorHandling;

public sealed class ProblemTypesTests
{
    private static ILocalizationService EchoKey()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.Get(Arg.Any<string>()).Returns(call => call.Arg<string>());
        return localization;
    }

    [Fact]
    public void EveryProblemTypeIsTheExactUrnTheFrontendMapsFrom()
    {
        Assert.Equal("urn:crm:problem:validation", ProblemTypes.Validation);
        Assert.Equal("urn:crm:problem:bad-request", ProblemTypes.BadRequest);
        Assert.Equal("urn:crm:problem:unauthorized", ProblemTypes.Unauthorized);
        Assert.Equal("urn:crm:problem:forbidden", ProblemTypes.Forbidden);
        Assert.Equal("urn:crm:problem:not-found", ProblemTypes.NotFound);
        Assert.Equal("urn:crm:problem:method-not-allowed", ProblemTypes.MethodNotAllowed);
        Assert.Equal("urn:crm:problem:conflict", ProblemTypes.Conflict);
        Assert.Equal("urn:crm:problem:unsupported-media-type", ProblemTypes.UnsupportedMediaType);
        Assert.Equal("urn:crm:problem:domain-rule", ProblemTypes.DomainRule);
        Assert.Equal("urn:crm:problem:rate-limited", ProblemTypes.RateLimited);
        Assert.Equal("urn:crm:problem:internal", ProblemTypes.Internal);
        Assert.Equal("urn:crm:problem:dependency-unavailable", ProblemTypes.DependencyUnavailable);
    }

    [Theory]
    [InlineData(400, "urn:crm:problem:bad-request")]
    [InlineData(401, "urn:crm:problem:unauthorized")]
    [InlineData(403, "urn:crm:problem:forbidden")]
    [InlineData(404, "urn:crm:problem:not-found")]
    [InlineData(405, "urn:crm:problem:method-not-allowed")]
    [InlineData(409, "urn:crm:problem:conflict")]
    [InlineData(415, "urn:crm:problem:unsupported-media-type")]
    [InlineData(422, "urn:crm:problem:domain-rule")]
    [InlineData(429, "urn:crm:problem:rate-limited")]
    [InlineData(503, "urn:crm:problem:dependency-unavailable")]
    [InlineData(500, "urn:crm:problem:internal")]
    [InlineData(501, "urn:crm:problem:internal")]
    [InlineData(502, "urn:crm:problem:internal")]
    [InlineData(599, "urn:crm:problem:internal")]
    [InlineData(200, "urn:crm:problem:bad-request")]
    [InlineData(302, "urn:crm:problem:bad-request")]
    [InlineData(418, "urn:crm:problem:bad-request")]
    public void ForStatusMapsAStatusToItsType(int status, string expected)
    {
        Assert.Equal(expected, ProblemTypes.ForStatus(status));
    }

    [Theory]
    [InlineData("urn:crm:problem:validation", "Problem.Title.validation")]
    [InlineData("urn:crm:problem:bad-request", "Problem.Title.bad-request")]
    [InlineData("urn:crm:problem:unauthorized", "Problem.Title.unauthorized")]
    [InlineData("urn:crm:problem:forbidden", "Problem.Title.forbidden")]
    [InlineData("urn:crm:problem:not-found", "Problem.Title.not-found")]
    [InlineData("urn:crm:problem:method-not-allowed", "Problem.Title.method-not-allowed")]
    [InlineData("urn:crm:problem:conflict", "Problem.Title.conflict")]
    [InlineData("urn:crm:problem:unsupported-media-type", "Problem.Title.unsupported-media-type")]
    [InlineData("urn:crm:problem:domain-rule", "Problem.Title.domain-rule")]
    [InlineData("urn:crm:problem:rate-limited", "Problem.Title.rate-limited")]
    [InlineData("urn:crm:problem:dependency-unavailable", "Problem.Title.dependency-unavailable")]
    [InlineData("urn:crm:problem:internal", "Problem.Title.internal")]
    public void TitleForAsksTheLocaliserForTheMatchingTitleKey(string type, string expectedKey)
    {
        Assert.Equal(expectedKey, ProblemTypes.TitleFor(type, EchoKey()));
    }

    [Fact]
    public void TheTitleKeysAreTheCrmMessageKeyConstants()
    {
        var localization = EchoKey();

        Assert.Equal(CrmMessageKeys.ProblemTitle.Validation, ProblemTypes.TitleFor(ProblemTypes.Validation, localization));
        Assert.Equal(CrmMessageKeys.ProblemTitle.RateLimited, ProblemTypes.TitleFor(ProblemTypes.RateLimited, localization));
    }

    [Theory]
    [InlineData("urn:crm:problem:something-new")]
    [InlineData("")]
    [InlineData("https://tools.ietf.org/html/rfc9110#section-15.5.1")]
    public void AnUnknownTypeGetsTheInternalErrorTitle(string type)
    {
        Assert.Equal(CrmMessageKeys.ProblemTitle.Internal, ProblemTypes.TitleFor(type, EchoKey()));
    }

    [Fact]
    public void TitleForRejectsANullLocaliser()
    {
        Func<string, ILocalizationService, string> method = ProblemTypes.TitleFor;

        NullArguments.AssertThrowsArgumentNull(method, ProblemTypes.Validation, null);
    }

    [Fact]
    public void EveryTypeIsAUrnNotADereferenceableUrl()
    {
        foreach (string type in new[]
                 {
                     ProblemTypes.Validation, ProblemTypes.BadRequest, ProblemTypes.Unauthorized, ProblemTypes.Forbidden,
                     ProblemTypes.NotFound, ProblemTypes.MethodNotAllowed, ProblemTypes.Conflict, ProblemTypes.UnsupportedMediaType,
                     ProblemTypes.DomainRule, ProblemTypes.RateLimited, ProblemTypes.Internal, ProblemTypes.DependencyUnavailable,
                 })
        {
            Assert.StartsWith("urn:crm:problem:", type, StringComparison.Ordinal);
        }
    }
}
