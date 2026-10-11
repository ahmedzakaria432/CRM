using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Localization;
using Crm.BuildingBlocks.Tests.Support;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.BuildingBlocks.Tests.Application.Authorization;

public sealed class RequestAuthorizationGuardTests
{
    // --- For ----------------------------------------------------------------------------------------------

    [Fact]
    public void AnAnonymousRequestAllowsAnonymousAndNamesNoPermission()
    {
        var requirement = RequestAuthorizationGuard.For(typeof(AnonymousCommand));

        Assert.Equal(new AuthorizationRequirement(true, null), requirement);
    }

    [Fact]
    public void APermissionRequestNamesItsPermission()
    {
        var requirement = RequestAuthorizationGuard.For(typeof(ReadQuery));

        Assert.Equal(new AuthorizationRequirement(false, CrmPermissions.TicketsRead), requirement);
    }

    [Fact]
    public void AnAuthenticatedOnlyRequestNamesNoPermission()
    {
        Assert.Equal(new AuthorizationRequirement(false, null), RequestAuthorizationGuard.For(typeof(AuthenticatedOnlyQuery)));
    }

    [Fact]
    public void ARequestWithNoAttributeIsNotAnonymousAndNamesNoPermission()
    {
        Assert.Equal(new AuthorizationRequirement(false, null), RequestAuthorizationGuard.For(typeof(UndeclaredRequest)));
    }

    [Fact]
    public void AttributesAreNotInherited()
    {
        Assert.Equal(new AuthorizationRequirement(false, null), RequestAuthorizationGuard.For(typeof(DerivedFromDeclaredRequest)));
    }

    [Fact]
    public void ForRejectsANullType()
    {
        Func<Type, AuthorizationRequirement> method = RequestAuthorizationGuard.For;

        NullArguments.AssertThrowsArgumentNull(method, new object?[] { null });
    }

    // --- EnsureEveryRequestDeclares -----------------------------------------------------------------------

    [Fact]
    public void TheBuildingBlocksApplicationAssemblyDeclaresNoRequestsSoItPasses()
    {
        RequestAuthorizationGuard.EnsureEveryRequestDeclares([typeof(CrmCultures).Assembly]);
    }

    [Fact]
    public void AnEmptyAssemblyListPasses()
    {
        RequestAuthorizationGuard.EnsureEveryRequestDeclares([]);
    }

    [Fact]
    public void ANullAssemblyListIsRefused()
    {
        Action<IEnumerable<System.Reflection.Assembly>> method = RequestAuthorizationGuard.EnsureEveryRequestDeclares;

        NullArguments.AssertThrowsArgumentNull(method, new object?[] { null });
    }

    [Fact]
    public void ARequestWithoutAnyDeclarationFails()
    {
        var builder = new DynamicAssemblyBuilder();
        builder.Request("Dyn.NoDeclaration");

        var exception = Assert.Throws<InvalidOperationException>(
            () => RequestAuthorizationGuard.EnsureEveryRequestDeclares([builder.Assembly]));

        Assert.Contains("Dyn.NoDeclaration: declare exactly one of", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARequestWithTwoDeclarationsFails()
    {
        var builder = new DynamicAssemblyBuilder();
        builder.Request(
            "Dyn.TwoDeclarations",
            null,
            DynamicAssemblyBuilder.RequirePermission(CrmPermissions.TicketsRead),
            DynamicAssemblyBuilder.RequireAuthenticatedUser());

        var exception = Assert.Throws<InvalidOperationException>(
            () => RequestAuthorizationGuard.EnsureEveryRequestDeclares([builder.Assembly]));

        Assert.Contains("Dyn.TwoDeclarations: declare exactly one of", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARequestNamingAnUnknownPermissionFails()
    {
        var builder = new DynamicAssemblyBuilder();
        builder.Request("Dyn.UnknownPermission", null, DynamicAssemblyBuilder.RequirePermission("tickets.fly"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => RequestAuthorizationGuard.EnsureEveryRequestDeclares([builder.Assembly]));

        Assert.Contains("Dyn.UnknownPermission: unknown permission 'tickets.fly'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARequestThatDoesNotReturnAResultFails()
    {
        var builder = new DynamicAssemblyBuilder();
        builder.Request("Dyn.NotAResult", typeof(string), DynamicAssemblyBuilder.RequireAuthenticatedUser());

        var exception = Assert.Throws<InvalidOperationException>(
            () => RequestAuthorizationGuard.EnsureEveryRequestDeclares([builder.Assembly]));

        Assert.Contains("Dyn.NotAResult: must return Result or Result<T>", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AGenericResultResponseIsAccepted()
    {
        var builder = new DynamicAssemblyBuilder();
        builder.Request("Dyn.ValueResult", typeof(Result<int>), DynamicAssemblyBuilder.RequireAuthenticatedUser());

        RequestAuthorizationGuard.EnsureEveryRequestDeclares([builder.Assembly]);
    }

    [Fact]
    public void AWellDeclaredRequestPasses()
    {
        var builder = new DynamicAssemblyBuilder();
        builder.Request("Dyn.Good", null, DynamicAssemblyBuilder.RequirePermission(CrmPermissions.TicketsWrite));
        builder.Request("Dyn.GoodAnonymous", null, DynamicAssemblyBuilder.AllowAnonymous());

        RequestAuthorizationGuard.EnsureEveryRequestDeclares([builder.Assembly]);
    }

    [Fact]
    public void AbstractInterfaceAndOpenGenericRequestsAreIgnored()
    {
        var builder = new DynamicAssemblyBuilder();
        builder.AbstractRequest("Dyn.AbstractBase");
        builder.InterfaceRequest("Dyn.IMarker");
        builder.OpenGenericRequest("Dyn.OpenGeneric");

        RequestAuthorizationGuard.EnsureEveryRequestDeclares([builder.Assembly]);
    }

    [Fact]
    public void TheMessageNamesEveryOffendingTypeAndEveryProblem()
    {
        var builder = new DynamicAssemblyBuilder();
        builder.Request("Dyn.First");
        builder.Request("Dyn.Second", typeof(string), DynamicAssemblyBuilder.RequirePermission("nope"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => RequestAuthorizationGuard.EnsureEveryRequestDeclares([builder.Assembly]));

        Assert.Contains("Every MediatR request must declare its authorisation", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Dyn.First: declare exactly one", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Dyn.Second: unknown permission 'nope'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Dyn.Second: must return Result or Result<T>", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProblemsAreCollectedAcrossAssemblies()
    {
        var first = new DynamicAssemblyBuilder();
        first.Request("Dyn.FromFirst", null, DynamicAssemblyBuilder.RequirePermission("nope"));
        var second = new DynamicAssemblyBuilder();
        second.Request("Dyn.FromSecond");

        var exception = Assert.Throws<InvalidOperationException>(
            () => RequestAuthorizationGuard.EnsureEveryRequestDeclares([first.Assembly, second.Assembly]));

        Assert.Contains("Dyn.FromFirst", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Dyn.FromSecond", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AttributeUsageIsSingleUseAndNotInherited()
    {
        foreach (Type attribute in new[]
                 {
                     typeof(RequirePermissionAttribute), typeof(RequireAuthenticatedUserAttribute), typeof(AllowAnonymousRequestAttribute),
                 })
        {
            var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(attribute, typeof(AttributeUsageAttribute));

            Assert.NotNull(usage);
            Assert.False(usage.AllowMultiple);
            Assert.False(usage.Inherited);
            Assert.Equal(AttributeTargets.Class, usage.ValidOn);
        }
    }

    [Fact]
    public void RequirePermissionExposesItsPermission()
    {
        Assert.Equal("x.y", new RequirePermissionAttribute("x.y").Permission);
    }

    [RequirePermission(CrmPermissions.TicketsRead)]
    private record DeclaredRequest : IRequest<Result>;

    private sealed record DerivedFromDeclaredRequest : DeclaredRequest;

    private sealed record UndeclaredRequest : IRequest<Result>;
}
