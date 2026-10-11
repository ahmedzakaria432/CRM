using Crm.SharedKernel.Scoping;

namespace Crm.SharedKernel.Tests.Scoping;

public sealed class ScopeMarkersTests
{
    [Fact]
    public void OrganisationScopedExtendsTenantScoped()
    {
        Assert.True(typeof(ITenantScoped).IsAssignableFrom(typeof(IOrganisationScoped)));
    }

    [Fact]
    public void TransferableExtendsOrganisationScoped()
    {
        Assert.True(typeof(IOrganisationScoped).IsAssignableFrom(typeof(ITransferableOrganisationScoped)));
    }

    [Fact]
    public void PortalCustomerOwnedExtendsOrganisationScoped()
    {
        Assert.True(typeof(IOrganisationScoped).IsAssignableFrom(typeof(IPortalCustomerOwned)));
    }

    [Fact]
    public void PortalCustomerOwnedExposesACustomerId()
    {
        var property = typeof(IPortalCustomerOwned).GetProperty(nameof(IPortalCustomerOwned.CustomerId));

        Assert.NotNull(property);
        Assert.Equal(typeof(Guid), property.PropertyType);
    }

    [Fact]
    public void TransferableAndPortalOwnedAreSiblingsNotParentAndChild()
    {
        Assert.False(typeof(ITransferableOrganisationScoped).IsAssignableFrom(typeof(IPortalCustomerOwned)));
        Assert.False(typeof(IPortalCustomerOwned).IsAssignableFrom(typeof(ITransferableOrganisationScoped)));
    }

    [Fact]
    public void TenantScopedExposesATenantId()
    {
        var property = typeof(ITenantScoped).GetProperty(nameof(ITenantScoped.TenantId));

        Assert.NotNull(property);
        Assert.Equal(typeof(TenantId), property.PropertyType);
    }

    [Fact]
    public void OrganisationScopedExposesBranchAndDepartment()
    {
        Assert.Equal(typeof(BranchId), typeof(IOrganisationScoped).GetProperty(nameof(IOrganisationScoped.BranchId))?.PropertyType);
        Assert.Equal(typeof(DepartmentId), typeof(IOrganisationScoped).GetProperty(nameof(IOrganisationScoped.DepartmentId))?.PropertyType);
    }
}
