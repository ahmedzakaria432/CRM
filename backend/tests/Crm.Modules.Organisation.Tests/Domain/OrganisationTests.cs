using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel;
using Crm.SharedKernel.Scoping;
using OrganisationAggregate = Crm.Modules.Organisation.Domain.Organisation;

namespace Crm.Modules.Organisation.Tests.Domain;

public sealed class OrganisationTests
{
    private static Branding Other() => Brandings.Valid(english: "Other");

    [Fact]
    public void CreateUsesTheSuppliedTenantId()
    {
        OrganisationAggregate organisation = Organisations.New(tenant: Ids.TenantB);

        Assert.Equal(Ids.TenantB, organisation.Id);
        Assert.Equal("DEMO", organisation.Code.Value);
        Assert.True(organisation.IsActive);
    }

    [Fact]
    public void CreateGeneratesATenantIdWhenNoneIsSupplied()
    {
        OrganisationAggregate organisation = OrganisationAggregate.Create(OrganisationCode.Create("NEW"), Names.Text());

        Assert.NotEqual(Guid.Empty, organisation.Id.Value);
    }

    [Fact]
    public void ANewOrganisationHasNoBranding()
    {
        OrganisationAggregate organisation = Organisations.New();

        Assert.Null(organisation.Branding);
        Assert.Null(organisation.PreviousBranding);
        Assert.False(organisation.CanRevertBranding);
    }

    [Fact]
    public void TheFirstSaveKeepsNoPreviousButCanRevert()
    {
        OrganisationAggregate organisation = Organisations.New();
        Branding branding = Brandings.Valid();

        organisation.ChangeBranding(branding);

        Assert.Equal(branding, organisation.Branding);
        Assert.Null(organisation.PreviousBranding);
        Assert.True(organisation.CanRevertBranding);
    }

    [Fact]
    public void ASecondDifferentSaveMovesTheCurrentBrandingToPrevious()
    {
        OrganisationAggregate organisation = Organisations.New();
        Branding first = Brandings.Valid();
        Branding second = Other();
        organisation.ChangeBranding(first);

        organisation.ChangeBranding(second);

        Assert.Equal(second, organisation.Branding);
        Assert.Equal(first, organisation.PreviousBranding);
    }

    [Fact]
    public void SavingAnEqualBrandingIsANoOpThatKeepsThePreviousVersion()
    {
        OrganisationAggregate organisation = Organisations.New();
        Branding first = Brandings.Valid();
        Branding second = Other();
        organisation.ChangeBranding(first);
        organisation.ChangeBranding(second);

        organisation.ChangeBranding(Other());

        Assert.Equal(second, organisation.Branding);
        Assert.Equal(first, organisation.PreviousBranding);
        Assert.True(organisation.CanRevertBranding);
    }

    [Fact]
    public void SavingTheFirstBrandingTwiceKeepsTheDefaultsAsPrevious()
    {
        OrganisationAggregate organisation = Organisations.New();
        organisation.ChangeBranding(Brandings.Valid());

        organisation.ChangeBranding(Brandings.Valid());

        Assert.Null(organisation.PreviousBranding);
        Assert.True(organisation.CanRevertBranding);
    }

    [Fact]
    public void ChangeBrandingRejectsNull()
    {
        OrganisationAggregate organisation = Organisations.New();
        Action<Branding> change = organisation.ChangeBranding;

        NullArguments.AssertThrowsArgumentNull(change, new object?[] { null });
    }

    [Fact]
    public void RevertAfterTheFirstSaveRestoresTheDefaults()
    {
        OrganisationAggregate organisation = Organisations.New();
        Branding branding = Brandings.Valid();
        organisation.ChangeBranding(branding);

        organisation.RevertBranding();

        Assert.Null(organisation.Branding);
        Assert.Equal(branding, organisation.PreviousBranding);
        Assert.True(organisation.CanRevertBranding);
    }

    [Fact]
    public void RevertingTwiceReappliesTheBranding()
    {
        OrganisationAggregate organisation = Organisations.New();
        Branding branding = Brandings.Valid();
        organisation.ChangeBranding(branding);

        organisation.RevertBranding();
        organisation.RevertBranding();

        Assert.Equal(branding, organisation.Branding);
        Assert.Null(organisation.PreviousBranding);
    }

    [Fact]
    public void RevertWithNoHistoryIsRefused()
    {
        OrganisationAggregate organisation = Organisations.New();

        var exception = Assert.Throws<DomainException>(organisation.RevertBranding);

        Assert.Equal("Organisation.NoPreviousBranding", exception.Error.Code);
    }

    [Fact]
    public void ARevertAfterANoOpSaveRestoresTheRightVersion()
    {
        OrganisationAggregate organisation = Organisations.New();
        Branding first = Brandings.Valid();
        organisation.ChangeBranding(first);
        organisation.ChangeBranding(Other());
        organisation.ChangeBranding(Other());

        organisation.RevertBranding();

        Assert.Equal(first, organisation.Branding);
    }

    [Fact]
    public void AnOrganisationIsNotTenantScoped()
    {
        Assert.IsNotAssignableFrom<ITenantScoped>(Organisations.New());
    }

    [Fact]
    public void NoDomainEventIsRaised()
    {
        OrganisationAggregate organisation = Organisations.New();
        organisation.ChangeBranding(Brandings.Valid());
        organisation.ChangeBranding(Other());
        organisation.RevertBranding();

        Assert.Empty(organisation.DomainEvents);
    }
}
