using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Tests.Support;

namespace Crm.Modules.Organisation.Tests.Application;

public sealed class OrganisationCacheKeysTests
{
    [Fact]
    public void ConstantsFollowTheModuleAggregateIdPattern()
    {
        Assert.Equal("organisation:brandings", OrganisationCacheKeys.Brandings);
        Assert.Equal("organisation:brandings:current", OrganisationCacheKeys.CurrentBranding);
    }

    [Theory]
    [InlineData("RYD", "organisation:brandings:code:RYD")]
    [InlineData(" ryd ", "organisation:brandings:code:RYD")]
    [InlineData("Demo-1", "organisation:brandings:code:DEMO-1")]
    public void PublicBrandingNormalisesTheCode(string code, string expected)
    {
        Assert.Equal(expected, OrganisationCacheKeys.PublicBranding(code));
    }

    [Fact]
    public void PublicBrandingRejectsNull()
    {
        Func<string, string> key = OrganisationCacheKeys.PublicBranding;

        NullArguments.AssertThrowsArgumentNull(key, new object?[] { null });
    }

    [Fact]
    public void EveryKeyStartsWithTheInvalidatedTag()
    {
        Assert.StartsWith(OrganisationCacheKeys.Brandings, OrganisationCacheKeys.CurrentBranding, StringComparison.Ordinal);
        Assert.StartsWith(OrganisationCacheKeys.Brandings, OrganisationCacheKeys.PublicBranding("x1"), StringComparison.Ordinal);
    }
}
