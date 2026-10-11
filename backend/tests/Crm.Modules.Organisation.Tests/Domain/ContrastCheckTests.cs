using Crm.Modules.Organisation.Domain;

namespace Crm.Modules.Organisation.Tests.Domain;

public sealed class ContrastCheckTests
{
    [Fact]
    public void PassesWhenTheRatioEqualsTheRequirement()
    {
        Assert.True(new ContrastCheck(ContrastCheck.Names.PrimaryText, 4.5, 4.5).Passes);
    }

    [Fact]
    public void PassesWhenTheRatioExceedsTheRequirement()
    {
        Assert.True(new ContrastCheck(ContrastCheck.Names.PrimaryText, 7, 4.5).Passes);
    }

    [Fact]
    public void FailsWhenJustUnderTheRequirement()
    {
        Assert.False(new ContrastCheck(ContrastCheck.Names.PrimaryText, 4.4999, 4.5).Passes);
    }

    [Fact]
    public void NamesAreTheApiContract()
    {
        Assert.Equal("PrimaryText", ContrastCheck.Names.PrimaryText);
        Assert.Equal("AccentOnSurface", ContrastCheck.Names.AccentOnSurface);
    }

    [Fact]
    public void ChecksAreEqualByValue()
    {
        Assert.Equal(new ContrastCheck("PrimaryText", 4.6, 4.5), new ContrastCheck("PrimaryText", 4.6, 4.5));
        Assert.NotEqual(new ContrastCheck("PrimaryText", 4.6, 4.5), new ContrastCheck("AccentOnSurface", 4.6, 4.5));
    }
}
