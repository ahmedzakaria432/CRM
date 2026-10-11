using Crm.BuildingBlocks.Application.Abstractions;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Identity.Tests.Support;

/// <summary><see cref="ICurrentUserService"/> substitutes whose string members agree with the real scope they carry.
/// The user and session ids are strings: handlers Guid.TryParse them.</summary>
public static class CurrentUsers
{
    public static readonly string DefaultUserId = Ids.User.ToString();
    public static readonly string DefaultSessionId = Ids.Session.ToString();

    public static ICurrentUserService Admin(TenantId? tenant = null) =>
        Build(Scopes.Agent(tenant ?? Ids.TenantA, Scopes.Member(Ids.DeptA1, Ids.BranchA1, CrmRoles.Administrator)), DefaultUserId, DefaultSessionId);

    public static ICurrentUserService Agent(OrganisationScope scope) => Build(scope, DefaultUserId, DefaultSessionId);

    public static ICurrentUserService WithIds(OrganisationScope scope, string? userId, string? sessionId) => Build(scope, userId, sessionId);

    public static ICurrentUserService Portal(TenantId tenant, Guid customerId, Guid contactId, bool isAdmin, string? userId = null, string? sessionId = null) =>
        Build(Scopes.Portal(tenant, customerId, contactId, isAdmin), userId ?? Ids.Account.ToString(), sessionId ?? DefaultSessionId);

    public static ICurrentUserService Anonymous() => Build(OrganisationScope.None, null, null);

    public static ICurrentUserService Unrestricted() => Build(OrganisationScope.Unrestricted, null, null);

    private static ICurrentUserService Build(OrganisationScope scope, string? userId, string? sessionId)
    {
        var user = Substitute.For<ICurrentUserService>();
        user.Scope.Returns(scope);
        user.UserId.Returns(userId);
        user.SessionId.Returns(sessionId);
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
