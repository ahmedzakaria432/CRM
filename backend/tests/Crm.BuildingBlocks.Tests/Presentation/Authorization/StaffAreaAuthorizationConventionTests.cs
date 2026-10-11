using Crm.BuildingBlocks.Presentation;
using Crm.BuildingBlocks.Presentation.Authorization;
using Crm.BuildingBlocks.Tests.Support;
using Microsoft.AspNetCore.Authorization;

namespace Crm.BuildingBlocks.Tests.Presentation.Authorization;

public sealed class StaffAreaAuthorizationConventionTests
{
    [Theory]
    [InlineData("Agent")]
    [InlineData("Admin")]
    public void EveryActionOfAnAgentOrAdminControllerRequiresTheStaffPolicy(string area)
    {
        var controller = ConventionFixtures.Controller(area, actions: 2, selectorsPerAction: 2);

        new StaffAreaAuthorizationConvention().Apply(controller);

        foreach (var selector in controller.Actions.SelectMany(a => a.Selectors))
        {
            var authorize = Assert.Single(selector.EndpointMetadata.OfType<AuthorizeAttribute>());
            Assert.Equal(CrmPolicies.Staff, authorize.Policy);
        }
    }

    [Theory]
    [InlineData("Portal")]
    [InlineData("Public")]
    [InlineData("Other")]
    public void OtherAreasAreUntouched(string area)
    {
        var controller = ConventionFixtures.Controller(area, actions: 2, selectorsPerAction: 1);

        new StaffAreaAuthorizationConvention().Apply(controller);

        Assert.All(controller.Actions.SelectMany(a => a.Selectors), s => Assert.Empty(s.EndpointMetadata));
    }

    [Fact]
    public void AControllerWithNoAreaIsUntouched()
    {
        var controller = ConventionFixtures.Controller(null, actions: 1, selectorsPerAction: 1);

        new StaffAreaAuthorizationConvention().Apply(controller);

        Assert.Empty(controller.Actions[0].Selectors[0].EndpointMetadata);
    }

    [Theory]
    [InlineData("agent")]
    [InlineData("ADMIN")]
    public void TheAreaIsComparedOrdinally(string area)
    {
        var controller = ConventionFixtures.Controller(area, actions: 1, selectorsPerAction: 1);

        new StaffAreaAuthorizationConvention().Apply(controller);

        Assert.Empty(controller.Actions[0].Selectors[0].EndpointMetadata);
    }

    [Fact]
    public void ExistingMetadataOnASelectorIsKept()
    {
        var controller = ConventionFixtures.Controller(CrmAreas.Agent, actions: 1, selectorsPerAction: 1);
        var allowAnonymous = new AllowAnonymousAttribute();
        controller.Actions[0].Selectors[0].EndpointMetadata.Add(allowAnonymous);

        new StaffAreaAuthorizationConvention().Apply(controller);

        Assert.Contains(allowAnonymous, controller.Actions[0].Selectors[0].EndpointMetadata);
        Assert.Contains(controller.Actions[0].Selectors[0].EndpointMetadata, m => m is AuthorizeAttribute);
    }

    [Fact]
    public void AControllerWithNoActionsIsHandled()
    {
        var controller = ConventionFixtures.Controller(CrmAreas.Agent, actions: 0, selectorsPerAction: 0);

        new StaffAreaAuthorizationConvention().Apply(controller);

        Assert.Empty(controller.Actions);
    }

    [Fact]
    public void ANullControllerIsRefused()
    {
        var convention = new StaffAreaAuthorizationConvention();

        NullArguments.AssertThrowsArgumentNull(convention.Apply, new object?[] { null });
    }
}
