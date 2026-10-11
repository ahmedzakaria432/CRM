using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Theming;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using OrganisationAggregate = Crm.Modules.Organisation.Domain.Organisation;

namespace Crm.Modules.Organisation.Tests.Application;

public sealed class DtosTests
{
    [Fact]
    public void LocalizedTextDtoMapsBothLanguages()
    {
        LocalizedTextDto dto = LocalizedTextDto.FromDomain(Names.Text("Riyadh", "الرياض"));

        Assert.Equal("الرياض", dto.Ar);
        Assert.Equal("Riyadh", dto.En);
    }

    [Fact]
    public void BranchDtoMapsEveryField()
    {
        Branch branch = Branches.New(code: "ryd", id: Ids.BranchA1, active: false);

        BranchDto dto = BranchDto.FromDomain(branch);

        Assert.Equal(Ids.BranchA1.Value, dto.Id);
        Assert.Equal("RYD", dto.Code);
        Assert.Equal("Riyadh", dto.Name.En);
        Assert.False(dto.IsActive);
        Assert.Same(branch.RowVersion, dto.RowVersion);
    }

    [Fact]
    public void DepartmentDtoMapsEveryField()
    {
        Department department = Departments.New(Branches.New(id: Ids.BranchA1), "care", id: Ids.DeptA1);

        DepartmentDto dto = DepartmentDto.FromDomain(department);

        Assert.Equal(Ids.DeptA1.Value, dto.Id);
        Assert.Equal(Ids.BranchA1.Value, dto.BranchId);
        Assert.Equal("CARE", dto.Code);
        Assert.Equal("Care", dto.Name.En);
        Assert.True(dto.IsActive);
        Assert.Same(department.RowVersion, dto.RowVersion);
    }

    [Fact]
    public void MembershipDtoMapsTheRoleAsAString()
    {
        Department department = Departments.New(Branches.New(id: Ids.BranchA1), id: Ids.DeptA1);
        Membership membership = Memberships.New(department, Ids.User, SystemRole.Administrator);

        MembershipDto dto = MembershipDto.FromDomain(membership);

        Assert.Equal(membership.Id.Value, dto.Id);
        Assert.Equal(Ids.User, dto.UserId);
        Assert.Equal(Ids.DeptA1.Value, dto.DepartmentId);
        Assert.Equal(Ids.BranchA1.Value, dto.BranchId);
        Assert.Equal("Administrator", dto.Role);
    }

    [Fact]
    public void BrandingDtoMapsHexFontAndLogoGuids()
    {
        BrandLogoId light = BrandLogoId.New();
        BrandLogoId dark = BrandLogoId.New();

        BrandingDto dto = BrandingDto.FromDomain(Brandings.Valid(light, dark, "Brand", ArabicFont.NotoKufiArabic));

        Assert.Equal("Brand", dto.DisplayName.En);
        Assert.Equal("#0E6655", dto.PrimaryColour);
        Assert.Equal("#B7791F", dto.AccentColour);
        Assert.Equal("NotoKufiArabic", dto.ArabicFont);
        Assert.Equal(light.Value, dto.LightLogoId);
        Assert.Equal(dark.Value, dto.DarkLogoId);
    }

    [Fact]
    public void BrandingDtoMapsNullLogosAsNull()
    {
        BrandingDto dto = BrandingDto.FromDomain(Brandings.Valid());

        Assert.Null(dto.LightLogoId);
        Assert.Null(dto.DarkLogoId);
    }

    [Fact]
    public void BrandingDtoDefaultsUseTheProductConstants()
    {
        BrandingDto dto = BrandingDto.Defaults(Names.Text("Org", "منشأة"));

        Assert.Equal("#145EC1", dto.PrimaryColour);
        Assert.Equal("#0F766E", dto.AccentColour);
        Assert.Equal(BrandDefaults.Primary, dto.PrimaryColour);
        Assert.Equal(BrandDefaults.Accent, dto.AccentColour);
        Assert.Equal("IbmPlexSansArabic", dto.ArabicFont);
        Assert.Equal("Org", dto.DisplayName.En);
        Assert.Null(dto.LightLogoId);
        Assert.Null(dto.DarkLogoId);
    }

    [Fact]
    public void TheBrandDefaultsAreValidColoursThatPassTheContrastRules()
    {
        BrandPalette palette = BrandPalette.Create(BrandColour.Create(BrandDefaults.Primary), BrandColour.Create(BrandDefaults.Accent));

        Assert.NotNull(palette);
    }

    [Fact]
    public void SettingsOfAnOrganisationNeverBranded()
    {
        OrganisationAggregate organisation = Organisations.New(name: "Demo");

        BrandingSettingsDto dto = BrandingSettingsDto.FromDomain(organisation);

        Assert.Equal("Demo", dto.OrganisationName.En);
        Assert.False(dto.IsCustom);
        Assert.False(dto.CanRevert);
        Assert.Null(dto.Previous);
        Assert.Equal("#145EC1", dto.Current.PrimaryColour);
        Assert.Equal("Demo", dto.Current.DisplayName.En);
    }

    [Fact]
    public void SettingsOfAnOrganisationBrandedOnce()
    {
        OrganisationAggregate organisation = Organisations.New();
        organisation.ChangeBranding(Brandings.Valid(english: "One"));

        BrandingSettingsDto dto = BrandingSettingsDto.FromDomain(organisation);

        Assert.True(dto.IsCustom);
        Assert.True(dto.CanRevert);
        Assert.Null(dto.Previous);
        Assert.Equal("One", dto.Current.DisplayName.En);
    }

    [Fact]
    public void SettingsOfAnOrganisationBrandedTwiceCarryThePreviousVersion()
    {
        OrganisationAggregate organisation = Organisations.New();
        organisation.ChangeBranding(Brandings.Valid(english: "One"));
        organisation.ChangeBranding(Brandings.Valid(english: "Two"));

        BrandingSettingsDto dto = BrandingSettingsDto.FromDomain(organisation);

        Assert.Equal("Two", dto.Current.DisplayName.En);
        Assert.NotNull(dto.Previous);
        Assert.Equal("One", dto.Previous.DisplayName.En);
    }

    [Fact]
    public void SettingsAfterARevertToTheDefaultsAreNotCustomButCanRevert()
    {
        OrganisationAggregate organisation = Organisations.New();
        organisation.ChangeBranding(Brandings.Valid(english: "One"));
        organisation.RevertBranding();

        BrandingSettingsDto dto = BrandingSettingsDto.FromDomain(organisation);

        Assert.False(dto.IsCustom);
        Assert.True(dto.CanRevert);
        Assert.NotNull(dto.Previous);
        Assert.Equal("One", dto.Previous.DisplayName.En);
        Assert.Equal("#145EC1", dto.Current.PrimaryColour);
    }

    [Fact]
    public void PublicBrandingOfABrandedOrganisationCarriesTokensAndLogos()
    {
        BrandLogoId light = BrandLogoId.New();
        OrganisationAggregate organisation = Organisations.New();
        organisation.ChangeBranding(Brandings.Valid(light, english: "Brand"));

        PublicBrandingDto dto = PublicBrandingDto.FromDomain(organisation);

        Assert.Equal("Brand", dto.DisplayName.En);
        Assert.Equal(light.Value, dto.LightLogoId);
        Assert.Null(dto.DarkLogoId);
        Assert.Equal("Tajawal", dto.ArabicFont);
        Assert.NotNull(dto.Tokens);
        Assert.Equal("#0E6655", dto.Tokens.Light["--crm-primary-600"]);
    }

    [Fact]
    public void PublicBrandingOfAnUnbrandedOrganisationKeepsTheDefaults()
    {
        PublicBrandingDto dto = PublicBrandingDto.FromDomain(Organisations.New(name: "Demo"));

        Assert.Equal("Demo", dto.DisplayName.En);
        Assert.Equal("IbmPlexSansArabic", dto.ArabicFont);
        Assert.Null(dto.Tokens);
        Assert.Null(dto.LightLogoId);
        Assert.Null(dto.DarkLogoId);
    }

    [Fact]
    public void FactoriesRejectNull()
    {
        Func<Branding, BrandingDto> branding = BrandingDto.FromDomain;
        Func<LocalizedText, BrandingDto> defaults = BrandingDto.Defaults;
        Func<OrganisationAggregate, BrandingSettingsDto> settings = BrandingSettingsDto.FromDomain;
        Func<OrganisationAggregate, PublicBrandingDto> publicBranding = PublicBrandingDto.FromDomain;

        NullArguments.AssertThrowsArgumentNull(branding, new object?[] { null });
        NullArguments.AssertThrowsArgumentNull(defaults, new object?[] { null });
        NullArguments.AssertThrowsArgumentNull(settings, new object?[] { null });
        NullArguments.AssertThrowsArgumentNull(publicBranding, new object?[] { null });
    }
}
