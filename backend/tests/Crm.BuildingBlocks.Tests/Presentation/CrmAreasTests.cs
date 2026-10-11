using Crm.BuildingBlocks.Presentation;

namespace Crm.BuildingBlocks.Tests.Presentation;

public sealed class CrmAreasTests
{
    [Fact]
    public void TheFourAreasAreNamedAfterTheirPersonas()
    {
        Assert.Equal("Agent", CrmAreas.Agent);
        Assert.Equal("Portal", CrmAreas.Portal);
        Assert.Equal("Admin", CrmAreas.Admin);
        Assert.Equal("Public", CrmAreas.Public);
    }

    [Fact]
    public void AllListsExactlyTheFourAreasInOrder()
    {
        Assert.Equal([CrmAreas.Agent, CrmAreas.Portal, CrmAreas.Admin, CrmAreas.Public], CrmAreas.All);
        Assert.Equal(4, CrmAreas.All.Count);
    }
}
