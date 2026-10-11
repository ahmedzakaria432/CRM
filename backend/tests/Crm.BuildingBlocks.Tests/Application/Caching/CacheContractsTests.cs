using Crm.BuildingBlocks.Application.Caching;
using Crm.BuildingBlocks.Tests.Support;

namespace Crm.BuildingBlocks.Tests.Application.Caching;

public sealed class CacheContractsTests
{
    [Fact]
    public void ACacheableQueryDefaultsToTheConfiguredDurationAndNoUserVariation()
    {
        ICacheableQuery query = new CacheableRequiredOnlyQuery();

        Assert.Null(query.CacheDuration);
        Assert.False(query.VaryByUser);
    }

    [Fact]
    public void ACacheableQueryCanOverrideDurationAndVaryByUser()
    {
        ICacheableQuery timed = new CacheableTimedQuery();
        ICacheableQuery perUser = new CacheableUserQuery();

        Assert.Equal(TimeSpan.FromMinutes(42), timed.CacheDuration);
        Assert.True(perUser.VaryByUser);
    }

    [Fact]
    public void TheDefaultExpirationIsFiveMinutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(5), CachingSettings.Default.DefaultExpiration);
    }

    [Fact]
    public void SettingsRecordsHaveValueEquality()
    {
        Assert.Equal(new CachingSettings(TimeSpan.FromSeconds(30)), new CachingSettings(TimeSpan.FromSeconds(30)));
        Assert.NotEqual(new CachingSettings(TimeSpan.FromSeconds(30)), new CachingSettings(TimeSpan.FromSeconds(31)));
    }

    [Fact]
    public void EntrySettingsKeepTheirExpirationAndTags()
    {
        var settings = new CacheEntrySettings(TimeSpan.FromMinutes(1), ["a", "b"]);

        Assert.Equal(TimeSpan.FromMinutes(1), settings.Expiration);
        Assert.Equal(["a", "b"], settings.Tags);
    }

    [Fact]
    public void AnInvalidatingCommandNamesItsTags()
    {
        var command = Assert.IsAssignableFrom<ICacheInvalidatingCommand>(new InvalidatingCommand());

        Assert.Equal(["customers:customers:1", "customers:customers:list"], command.CacheTagsToInvalidate);
    }
}
