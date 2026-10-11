using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Organisation.Tests.Domain;

public sealed class DepartmentTests
{
    [Fact]
    public void CreateCopiesTenantAndBranchFromTheBranch()
    {
        Branch branch = Branches.New(Ids.TenantA, id: Ids.BranchA1);

        Department department = Departments.New(branch, "care", id: Ids.DeptA1);

        Assert.Equal(Ids.TenantA, department.TenantId);
        Assert.Equal(Ids.BranchA1, department.BranchId);
        Assert.Equal(Ids.DeptA1, department.Id);
        Assert.Equal("CARE", department.Code.Value);
        Assert.True(department.IsActive);
    }

    [Fact]
    public void CreateGeneratesAnIdWhenNoneIsSupplied()
    {
        Assert.NotEqual(Guid.Empty, Departments.New(Branches.New()).Id.Value);
    }

    [Fact]
    public void CreateRefusesAnInactiveBranch()
    {
        Branch inactive = Branches.New(active: false);

        var exception = Assert.Throws<DomainException>(() => Departments.New(inactive));

        Assert.Equal("Organisation.BranchInactive", exception.Error.Code);
    }

    [Fact]
    public void CreateRejectsANullBranch()
    {
        Func<Branch, OrganisationCode, LocalizedText, DepartmentId?, Department> create = Department.Create;

        NullArguments.AssertThrowsArgumentNull(create, null, OrganisationCode.Create("CARE"), Names.Text(), null);
    }

    [Fact]
    public void RenameReplacesTheName()
    {
        Department department = Departments.New(Branches.New());

        department.Rename(Names.Text("Billing", "الفوترة"));

        Assert.Equal("Billing", department.Name.English);
    }

    [Fact]
    public void DeactivateFlipsIsActive()
    {
        Department department = Departments.New(Branches.New());

        department.Deactivate();

        Assert.False(department.IsActive);
    }

    [Fact]
    public void DeactivatingTwiceIsRefused()
    {
        Department department = Departments.New(Branches.New(), active: false);

        var exception = Assert.Throws<DomainException>(department.Deactivate);

        Assert.Equal("Organisation.DepartmentAlreadyInactive", exception.Error.Code);
    }

    [Fact]
    public void DeactivatingTheDepartmentLeavesTheBranchUntouched()
    {
        Branch branch = Branches.New();
        Department department = Departments.New(branch);

        department.Deactivate();

        Assert.True(branch.IsActive);
    }

    [Fact]
    public void NoDomainEventIsRaised()
    {
        Department department = Departments.New(Branches.New());
        department.Rename(Names.Text("X", "س"));
        department.Deactivate();

        Assert.Empty(department.DomainEvents);
    }

    [Fact]
    public void ADepartmentIsTenantScopedOnly()
    {
        Department department = Departments.New(Branches.New());

        Assert.IsAssignableFrom<ITenantScoped>(department);
        Assert.IsNotAssignableFrom<IOrganisationScoped>(department);
    }
}
