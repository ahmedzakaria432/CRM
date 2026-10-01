using Crm.Modules.Organisation.Application.Theming;
using Crm.Modules.Organisation.Domain;

namespace Crm.Modules.Organisation.Application;

// Primitives only - no SharedKernel id types cross into a DTO (keeps Swagger/pnpm gen:api shapes plain).
public sealed record LocalizedTextDto(string Ar, string En)
{
    public static LocalizedTextDto FromDomain(LocalizedText name) => new(name.Arabic, name.English);
}

public sealed record BranchDto(Guid Id, string Code, LocalizedTextDto Name, bool IsActive, byte[] RowVersion)
{
    public static BranchDto FromDomain(Branch branch) => new(
        branch.Id.Value, branch.Code.Value, LocalizedTextDto.FromDomain(branch.Name), branch.IsActive, branch.RowVersion);
}

public sealed record DepartmentDto(Guid Id, Guid BranchId, string Code, LocalizedTextDto Name, bool IsActive, byte[] RowVersion)
{
    public static DepartmentDto FromDomain(Department department) => new(
        department.Id.Value, department.BranchId.Value, department.Code.Value,
        LocalizedTextDto.FromDomain(department.Name), department.IsActive, department.RowVersion);
}

public sealed record MembershipDto(Guid Id, Guid UserId, Guid DepartmentId, Guid BranchId, string Role, byte[] RowVersion)
{
    public static MembershipDto FromDomain(Membership membership) => new(
        membership.Id.Value, membership.UserId, membership.DepartmentId.Value, membership.BranchId.Value,
        membership.Role.ToString(), membership.RowVersion);
}

public sealed record MyDepartmentDto(
    Guid DepartmentId,
    string DepartmentCode,
    LocalizedTextDto DepartmentName,
    Guid BranchId,
    LocalizedTextDto BranchName,
    string Role,
    bool IsActive);

public sealed record MyScopeDto(
    Guid UserId,
    Guid TenantId,
    Guid? ActiveDepartmentId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<MyDepartmentDto> Departments);

public sealed record BrandTokensDto(IReadOnlyDictionary<string, string> Light, IReadOnlyDictionary<string, string> Dark);

public sealed record BrandingDto(LocalizedTextDto DisplayName, string PrimaryColour, string AccentColour, string ArabicFont, Guid? LightLogoId, Guid? DarkLogoId)
{
    public static BrandingDto FromDomain(Branding branding)
    {
        ArgumentNullException.ThrowIfNull(branding);

        return new(
            LocalizedTextDto.FromDomain(branding.DisplayName), branding.Palette.Primary.Hex, branding.Palette.Accent.Hex,
            branding.ArabicFont.ToString(), branding.LightLogo?.Value, branding.DarkLogo?.Value);
    }

    /// <summary>The product defaults, shown in the admin form when nothing is saved (D10).</summary>
    public static BrandingDto Defaults(LocalizedText organisationName)
    {
        ArgumentNullException.ThrowIfNull(organisationName);

        return new(
            LocalizedTextDto.FromDomain(organisationName), BrandDefaults.Primary, BrandDefaults.Accent, nameof(Domain.ArabicFont.IbmPlexSansArabic), null, null);
    }
}

/// <summary>Admin view. Current is the effective branding (defaults when IsCustom is false); Previous null with CanRevert = the defaults.</summary>
public sealed record BrandingSettingsDto(LocalizedTextDto OrganisationName, BrandingDto Current, bool IsCustom, BrandingDto? Previous, bool CanRevert, byte[] RowVersion)
{
    public static BrandingSettingsDto FromDomain(Domain.Organisation organisation)
    {
        ArgumentNullException.ThrowIfNull(organisation);

        return new(
            LocalizedTextDto.FromDomain(organisation.Name),
            organisation.Branding is { } current ? BrandingDto.FromDomain(current) : BrandingDto.Defaults(organisation.Name),
            organisation.Branding is not null,
            organisation.PreviousBranding is { } previous ? BrandingDto.FromDomain(previous) : null,
            organisation.CanRevertBranding,
            organisation.RowVersion);
    }
}

/// <summary>Anonymous view (S1). Tokens null = keep tokens.css.</summary>
public sealed record PublicBrandingDto(LocalizedTextDto DisplayName, Guid? LightLogoId, Guid? DarkLogoId, string ArabicFont, BrandTokensDto? Tokens)
{
    public static PublicBrandingDto FromDomain(Domain.Organisation organisation)
    {
        ArgumentNullException.ThrowIfNull(organisation);

        return organisation.Branding is { } b
            ? new(LocalizedTextDto.FromDomain(b.DisplayName), b.LightLogo?.Value, b.DarkLogo?.Value, b.ArabicFont.ToString(),
                  BrandTokenGenerator.Generate(b.Palette.Primary, b.Palette.Accent))
            : new(LocalizedTextDto.FromDomain(organisation.Name), null, null, nameof(Domain.ArabicFont.IbmPlexSansArabic), null);
    }
}

public sealed record ContrastCheckDto(string Name, double Ratio, double Required, bool Passes);

public sealed record BrandingPreviewDto(BrandTokensDto Tokens, IReadOnlyList<ContrastCheckDto> Checks);

public sealed record BrandLogoDto(Guid Id, string ContentType, int SizeBytes);

public sealed record BrandLogoContentDto(byte[] Content, string ContentType);
