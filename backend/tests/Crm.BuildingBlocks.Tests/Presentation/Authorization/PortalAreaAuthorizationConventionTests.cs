using Crm.BuildingBlocks.Presentation;
using Crm.BuildingBlocks.Presentation.Authorization;
using Crm.BuildingBlocks.Tests.Support;
using Microsoft.AspNetCore.Authorization;

namespace Crm.BuildingBlocks.Tests.Presentation.Authorization;

public sealed class PortalAreaAuthorizationConventionTests
{
    [Fact]
    public void EveryActionOfAPortalControllerRequiresThePortalPolicy()
    {
        var controller = ConventionFixtures.Controller(CrmAreas.Portal, actions: 3, selectorsPerAction: 2);

        new PortalAreaAuthorizationConvention().Apply(controller);

        foreach (var selector in controller.Actions.SelectMany(a => a.Selectors))
        {
            var authorize = Assert.Single(selector.EndpointMetadata.OfType<AuthorizeAttribute>());
            Assert.Equal(CrmPolicies.Portal, authorize.Policy);
        }
    }

    [Theory]
    [InlineData("Agent")]
    [InlineData("Admin")]
    [InlineData("Public")]
    [InlineData("portal")]
    public void OtherAreasAreUntouched(string area)
    {
        var controller = ConventionFixtures.Controller(area, actions: 2, selectorsPerAction: 1);

        new PortalAreaAuthorizationConvention().Apply(controller);

        Assert.All(controller.Actions.SelectMany(a => a.Selectors), s => Assert.Empty(s.EndpointMetadata));
    }

    [Fact]
    public void AControllerWithNoAreaIsUntouched()
    {
        var controller = ConventionFixtures.Controller(null, actions: 1, selectorsPerAction: 1);

        new PortalAreaAuthorizationConvention().Apply(controller);

        Assert.Empty(controller.Actions[0].Selectors[0].EndpointMetadata);
    }

    [Fact]
    public void ExistingMetadataIsKept()
    {
        var controller = ConventionFixtures.Controller(CrmAreas.Portal, actions: 1, selectorsPerAction: 1);
        var allowAnonymous = new AllowAnonymousAttribute();
        controller.Actions[0].Selectors[0].EndpointMetadata.Add(allowAnonymous);

        new PortalAreaAuthorizationConvention().Apply(controller);

        Assert.Contains(allowAnonymous, controller.Actions[0].Selectors[0].EndpointMetadata);
    }

    [Fact]
    public void ANullControllerIsRefused()
    {
        var convention = new PortalAreaAuthorizationConvention();

        NullArguments.AssertThrowsArgumentNull(convention.Apply, new object?[] { null });
    }
}
