using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Organisation.Tests.Domain;

public sealed class BranchTests
{
    [Fact]
    public void CreateSetsTenantCodeNameAndIsActive()
    {
        Branch branch = Branches.New(Ids.TenantA, "ryd", name: "Riyadh");

        Assert.Equal(Ids.TenantA, branch.TenantId);
        Assert.Equal("RYD", branch.Code.Value);
        Assert.Equal("Riyadh", branch.Name.English);
        Assert.True(branch.IsActive);
        Assert.NotEqual(Guid.Empty, branch.Id.Value);
    }

    [Fact]
    public void CreateUsesTheSuppliedId()
    {
        Assert.Equal(Ids.BranchA1, Branches.New(id: Ids.BranchA1).Id);
    }

    [Fact]
    public void CreateRefusesTheNoneTenant()
    {
        var exception = Assert.Throws<DomainException>(() => Branches.New(TenantId.None));

        Assert.Equal("Organisation.TenantRequired", exception.Error.Code);
    }

    [Fact]
    public void RenameReplacesTheName()
    {
        Branch branch = Branches.New();

        branch.Rename(Names.Text("Jeddah", "جدة"));

        Assert.Equal("Jeddah", branch.Name.English);
        Assert.Equal("جدة", branch.Name.Arabic);
    }

    [Fact]
    public void DeactivateFlipsIsActive()
    {
        Branch branch = Branches.New();

        branch.Deactivate();

        Assert.False(branch.IsActive);
    }

    [Fact]
    public void DeactivatingTwiceIsRefused()
    {
        Branch branch = Branches.New(active: false);

        var exception = Assert.Throws<DomainException>(branch.Deactivate);

        Assert.Equal("Organisation.BranchAlreadyInactive", exception.Error.Code);
    }

    [Fact]
    public void ABranchIsTenantScoped()
    {
        Assert.IsAssignableFrom<ITenantScoped>(Branches.New());
        Assert.IsNotAssignableFrom<IOrganisationScoped>(Branches.New());
    }

    [Fact]
    public void NoDomainEventIsRaised()
    {
        Branch branch = Branches.New();
        branch.Rename(Names.Text("X", "س"));
        branch.Deactivate();

        Assert.Empty(branch.DomainEvents);
    }

    [Fact]
    public void BranchesAreEqualById()
    {
        Assert.Equal(Branches.New(id: Ids.BranchA1, code: "AA"), Branches.New(id: Ids.BranchA1, code: "BB"));
        Assert.NotEqual(Branches.New(id: Ids.BranchA1), Branches.New(id: Ids.BranchA2));
    }
}
