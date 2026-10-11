using Crm.SharedKernel.Scoping;

namespace Crm.SharedKernel.Tests.Scoping;

public sealed class ScopeIdsTests
{
    private static readonly Guid Raw = new("55555555-5555-5555-5555-555555555555");

    [Fact]
    public void TenantNoneIsTheEmptyGuid()
    {
        Assert.Equal(Guid.Empty, TenantId.None.Value);
    }

    [Fact]
    public void NewBranchIdsAreDistinctAndNonEmpty()
    {
        var first = BranchId.New();
        var second = BranchId.New();

        Assert.NotEqual(Guid.Empty, first.Value);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void NewDepartmentIdsAreDistinctAndNonEmpty()
    {
        var first = DepartmentId.New();
        var second = DepartmentId.New();

        Assert.NotEqual(Guid.Empty, first.Value);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void IdsOfDifferentScopeTypesWithTheSameGuidAreNotEqual()
    {
        StronglyTypedId tenant = new TenantId(Raw);
        StronglyTypedId branch = new BranchId(Raw);
        StronglyTypedId department = new DepartmentId(Raw);

        Assert.NotEqual(tenant, branch);
        Assert.NotEqual(branch, department);
        Assert.NotEqual(tenant, department);
    }

    [Fact]
    public void IdsOfTheSameTypeAndGuidAreEqual()
    {
        Assert.Equal(new TenantId(Raw), new TenantId(Raw));
        Assert.Equal(new BranchId(Raw), new BranchId(Raw));
        Assert.Equal(new DepartmentId(Raw), new DepartmentId(Raw));
    }

    [Fact]
    public void ToStringUsesTheSynthesizedRecordFormatNotTheBareGuid()
    {
        // See StronglyTypedIdTests: the base ToString override is hidden by each derived record's own.
        Assert.Equal("TenantId { Value = " + Raw + " }", new TenantId(Raw).ToString());
        Assert.Equal("BranchId { Value = " + Raw + " }", new BranchId(Raw).ToString());
        Assert.Equal("DepartmentId { Value = " + Raw + " }", new DepartmentId(Raw).ToString());
    }

    [Fact]
    public void ScopeIdsAreStronglyTypedIds()
    {
        Assert.IsAssignableFrom<StronglyTypedId>(TenantId.None);
        Assert.IsAssignableFrom<StronglyTypedId>(BranchId.New());
        Assert.IsAssignableFrom<StronglyTypedId>(DepartmentId.New());
    }

    [Fact]
    public void TheNoneInstanceEqualsAnEmptyTenantId()
    {
        Assert.Equal(TenantId.None, new TenantId(Guid.Empty));
    }
}
