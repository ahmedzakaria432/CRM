using Crm.BuildingBlocks.Application.Abstractions;

namespace Crm.Modules.Tickets.Tests.Support;

/// <summary><see cref="ICurrentUserService"/> substitutes whose string members agree with the real scope they carry.
/// The user id is a string: the handlers Guid.TryParse it.</summary>
public static class CurrentUsers
{
    public static readonly string DefaultUserId = Ids.Actor.ToString();

    public static ICurrentUserService Agent(OrganisationScope scope) => Build(scope, DefaultUserId);

    public static ICurrentUserService WithUserId(OrganisationScope scope, string? userId) => Build(scope, userId);

    public static ICurrentUserService Anonymous() => Build(OrganisationScope.None, null);

    public static ICurrentUserService Unrestricted() => Build(OrganisationScope.Unrestricted, null);

    /// <summary>A portal customer: the scope carries the grant and the user id is the contact id.</summary>
    public static ICurrentUserService Portal(PortalCustomerGrant grant) =>
        Build(Scopes.Portal(grant), grant.ContactId.ToString());

    private static ICurrentUserService Build(OrganisationScope scope, string? userId)
    {
        var user = Substitute.For<ICurrentUserService>();
        user.Scope.Returns(scope);
        user.UserId.Returns(userId);
        user.UserIdOrSystem.Returns(userId ?? "system");
        user.TenantId.Returns(scope.Tenant.Value == Guid.Empty ? null : scope.Tenant.Value.ToString());
        user.DepartmentId.Returns(scope.Departments.Count == 0
            ? null
            : string.Join(',', scope.Departments.Select(d => d.Value)));
        user.BranchId.Returns(scope.Branches.Count == 0
            ? null
            : string.Join(',', scope.Branches.Select(b => b.Value)));
        return user;
    }
}
