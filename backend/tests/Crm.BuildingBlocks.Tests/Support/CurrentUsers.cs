using Crm.BuildingBlocks.Application.Abstractions;

namespace Crm.BuildingBlocks.Tests.Support;

/// <summary><see cref="ICurrentUserService"/> substitutes whose string members agree with the real scope they carry.</summary>
public static class CurrentUsers
{
    public const string DefaultUserId = "user-1";

    public static ICurrentUserService Agent(OrganisationScope scope, string userId = DefaultUserId) =>
        Build(scope, userId);

    public static ICurrentUserService Portal(OrganisationScope scope, string userId = "portal-user-1") =>
        Build(scope, userId);

    public static ICurrentUserService Anonymous() => Build(OrganisationScope.None, null);

    public static ICurrentUserService Unrestricted() => Build(OrganisationScope.Unrestricted, null);

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
