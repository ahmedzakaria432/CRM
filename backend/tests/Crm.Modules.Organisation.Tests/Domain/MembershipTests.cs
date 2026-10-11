using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Organisation.Tests.Domain;

public sealed class MembershipTests
{
    private static Department ActiveDepartment() =>
        Departments.New(Branches.New(Ids.TenantA, id: Ids.BranchA1), id: Ids.DeptA1);

    [Fact]
    public void CreateCopiesTenantAndBranchFromTheDepartment()
    {
        Department department = ActiveDepartment();

        Membership membership = Memberships.New(department, Ids.User, SystemRole.Supervisor);

        Assert.Equal(Ids.TenantA, membership.TenantId);
        Assert.Equal(Ids.BranchA1, membership.BranchId);
        Assert.Equal(Ids.DeptA1, membership.DepartmentId);
        Assert.Equal(Ids.User, membership.UserId);
        Assert.Equal(SystemRole.Supervisor, membership.Role);
    }

    [Fact]
    public void CreateUsesTheSuppliedIdOrGeneratesOne()
    {
        var id = MembershipId.New();

        Assert.Equal(id, Memberships.New(ActiveDepartment(), id: id).Id);
        Assert.NotEqual(Guid.Empty, Memberships.New(ActiveDepartment()).Id.Value);
    }

    [Fact]
    public void CreateRefusesAnEmptyUser()
    {
        var exception = Assert.Throws<DomainException>(() => Membership.Create(ActiveDepartment(), Guid.Empty, SystemRole.Agent));

        Assert.Equal("Organisation.UserRequired", exception.Error.Code);
    }

    [Fact]
    public void CreateRefusesAnInactiveDepartment()
    {
        Department inactive = Departments.New(Branches.New(), active: false);

        var exception = Assert.Throws<DomainException>(() => Memberships.New(inactive));

        Assert.Equal("Organisation.DepartmentInactive", exception.Error.Code);
    }

    [Fact]
    public void AnEmptyUserIsReportedBeforeAnInactiveDepartment()
    {
        Department inactive = Departments.New(Branches.New(), active: false);

        var exception = Assert.Throws<DomainException>(() => Membership.Create(inactive, Guid.Empty, SystemRole.Agent));

        Assert.Equal("Organisation.UserRequired", exception.Error.Code);
    }

    [Fact]
    public void CreateRejectsANullDepartment()
    {
        Func<Department, Guid, SystemRole, MembershipId?, Membership> create = Membership.Create;

        NullArguments.AssertThrowsArgumentNull(create, null, Ids.User, SystemRole.Agent, null);
    }

    [Theory]
    [InlineData(SystemRole.Administrator)]
    [InlineData(SystemRole.Supervisor)]
    [InlineData(SystemRole.Agent)]
    public void ChangeRoleAcceptsEveryRole(SystemRole role)
    {
        Membership membership = Memberships.New(ActiveDepartment(), role: role == SystemRole.Agent ? SystemRole.Administrator : SystemRole.Agent);

        membership.ChangeRole(role);

        Assert.Equal(role, membership.Role);
    }

    [Fact]
    public void ANewMembershipIsNotSoftDeleted()
    {
        Membership membership = Memberships.New(ActiveDepartment());

        Assert.False(membership.IsDeleted);
        Assert.Null(membership.DeletedAtUtc);
        Assert.Null(membership.DeletedBy);
    }

    [Fact]
    public void AMembershipIsTenantScopedAndSoftDeletable()
    {
        Membership membership = Memberships.New(ActiveDepartment());

        Assert.IsAssignableFrom<ITenantScoped>(membership);
        Assert.IsAssignableFrom<ISoftDeletable>(membership);
    }

    [Fact]
    public void NoDomainEventIsRaised()
    {
        Membership membership = Memberships.New(ActiveDepartment());
        membership.ChangeRole(SystemRole.Supervisor);

        Assert.Empty(membership.DomainEvents);
    }
}
