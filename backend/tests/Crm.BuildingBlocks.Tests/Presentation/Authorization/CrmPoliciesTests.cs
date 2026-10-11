using System.Security.Claims;
using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Presentation.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.BuildingBlocks.Tests.Presentation.Authorization;

public sealed class CrmPoliciesTests
{
    private static ServiceProvider Provider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCrmAuthorization();
        return services.BuildServiceProvider();
    }

    private static ClaimsPrincipal Principal(params string?[] schemes) =>
        new(schemes.Select(s => new ClaimsIdentity([new Claim("sub", "u-1")], s)));

    private static async Task<bool> SucceedsAsync(string policy, ClaimsPrincipal user)
    {
        using var provider = Provider();
        var result = await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, policy);
        return result.Succeeded;
    }

    [Fact]
    public void ThePolicyNamesAreStable()
    {
        Assert.Equal("Staff", CrmPolicies.Staff);
        Assert.Equal("Portal", CrmPolicies.Portal);
    }

    [Theory]
    [InlineData("Staff")]
    [InlineData("Portal")]
    public async Task BothPoliciesListBothSchemesAndRequireAnAuthenticatedUser(string name)
    {
        using var provider = Provider();

        var policy = await provider.GetRequiredService<IAuthorizationPolicyProvider>().GetPolicyAsync(name);

        Assert.NotNull(policy);
        Assert.Equal([CrmAuthenticationSchemes.Staff, CrmAuthenticationSchemes.Portal], policy.AuthenticationSchemes);
        Assert.Contains(policy.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public async Task AStaffIdentitySatisfiesTheStaffPolicyButNotThePortalPolicy()
    {
        var user = Principal(CrmAuthenticationSchemes.Staff);

        Assert.True(await SucceedsAsync(CrmPolicies.Staff, user));
        Assert.False(await SucceedsAsync(CrmPolicies.Portal, user));
    }

    [Fact]
    public async Task APortalIdentitySatisfiesThePortalPolicyButNotTheStaffPolicy()
    {
        var user = Principal(CrmAuthenticationSchemes.Portal);

        Assert.True(await SucceedsAsync(CrmPolicies.Portal, user));
        Assert.False(await SucceedsAsync(CrmPolicies.Staff, user));
    }

    [Fact]
    public async Task AnAnonymousPrincipalFailsBoth()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.False(await SucceedsAsync(CrmPolicies.Staff, user));
        Assert.False(await SucceedsAsync(CrmPolicies.Portal, user));
    }

    [Fact]
    public async Task APrincipalWithBothIdentitiesSatisfiesBothPolicies()
    {
        var user = Principal(CrmAuthenticationSchemes.Staff, CrmAuthenticationSchemes.Portal);

        Assert.True(await SucceedsAsync(CrmPolicies.Staff, user));
        Assert.True(await SucceedsAsync(CrmPolicies.Portal, user));
    }

    [Theory]
    [InlineData("staff")]
    [InlineData("STAFF")]
    [InlineData("Bearer")]
    public async Task SchemeNamesAreComparedOrdinally(string scheme)
    {
        var user = Principal(scheme);

        Assert.False(await SucceedsAsync(CrmPolicies.Staff, user));
        Assert.False(await SucceedsAsync(CrmPolicies.Portal, user));
    }

    [Fact]
    public void ItReturnsTheCollectionForChaining()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddCrmAuthorization());
    }
}
