using Crm.Modules.Organisation.Domain;

namespace Crm.Modules.Organisation.Tests.Domain;

public sealed class ModuleIdsAndEnumsTests
{
    [Fact]
    public void NewBrandLogoIdsAreNonEmptyAndDistinct()
    {
        BrandLogoId first = BrandLogoId.New();
        BrandLogoId second = BrandLogoId.New();

        Assert.NotEqual(Guid.Empty, first.Value);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void NewMembershipIdsAreNonEmptyAndDistinct()
    {
        MembershipId first = MembershipId.New();
        MembershipId second = MembershipId.New();

        Assert.NotEqual(Guid.Empty, first.Value);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void IdsWithTheSameGuidAreEqual()
    {
        var guid = Guid.NewGuid();

        Assert.Equal(new BrandLogoId(guid), new BrandLogoId(guid));
        Assert.Equal(new MembershipId(guid), new MembershipId(guid));
    }

    [Fact]
    public void ArabicFontNamesAndValuesAreTheApiAndStorageContract()
    {
        Assert.Equal(["IbmPlexSansArabic", "NotoKufiArabic", "Tajawal"], Enum.GetNames<ArabicFont>());
        Assert.Equal(0, (int)ArabicFont.IbmPlexSansArabic);
        Assert.Equal(1, (int)ArabicFont.NotoKufiArabic);
        Assert.Equal(2, (int)ArabicFont.Tajawal);
    }

    [Fact]
    public void SystemRoleNamesAreTheRoleContract()
    {
        Assert.Equal(["Administrator", "Supervisor", "Agent"], Enum.GetNames<SystemRole>());
    }

    [Fact]
    public void LogoContentTypeValuesAreTheStorageContract()
    {
        Assert.Equal(["Png", "Svg"], Enum.GetNames<LogoContentType>());
        Assert.Equal(0, (int)LogoContentType.Png);
        Assert.Equal(1, (int)LogoContentType.Svg);
    }
}
