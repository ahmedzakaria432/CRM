using System.Text;
using Crm.Modules.Organisation.Domain;
using Crm.SharedKernel.Scoping;
using OrganisationAggregate = Crm.Modules.Organisation.Domain.Organisation;

namespace Crm.Modules.Organisation.Tests.Support;

public static class Names
{
    public static LocalizedText Text(string english = "Name", string arabic = "اسم") => LocalizedText.Create(arabic, english);
}

public static class Organisations
{
    public static OrganisationAggregate New(string code = "DEMO", string name = "Demo", TenantId? tenant = null)
    {
        return OrganisationAggregate.Create(OrganisationCode.Create(code), Names.Text(name), tenant ?? Ids.TenantA);
    }
}

public static class Branches
{
    public static Branch New(TenantId? tenant = null, string code = "RYD", bool active = true, BranchId? id = null, string name = "Riyadh")
    {
        Branch branch = Branch.Create(tenant ?? Ids.TenantA, OrganisationCode.Create(code), Names.Text(name), id);
        if (!active)
        {
            branch.Deactivate();
        }

        return branch;
    }
}

public static class Departments
{
    public static Department New(Branch branch, string code = "CARE", bool active = true, DepartmentId? id = null, string name = "Care")
    {
        Department department = Department.Create(branch, OrganisationCode.Create(code), Names.Text(name), id);
        if (!active)
        {
            department.Deactivate();
        }

        return department;
    }
}

public static class Memberships
{
    public static Membership New(Department department, Guid? userId = null, SystemRole role = SystemRole.Agent, MembershipId? id = null) =>
        Membership.Create(department, userId ?? Ids.User, role, id);
}

public static class Colours
{
    public static BrandColour Primary => BrandColour.Create("#0E6655");

    public static BrandColour Accent => BrandColour.Create("#B7791F");
}

public static class Logos
{
    public const string SvgText = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\"><rect width=\"10\" height=\"10\"/></svg>";

    public static byte[] PngBytes(int size = 16)
    {
        byte[] bytes = new byte[size];
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(bytes, 0);
        return bytes;
    }

    public static byte[] SvgBytes(string text = SvgText) => Encoding.UTF8.GetBytes(text);

    public static BrandLogo Png(TenantId? tenant = null, BrandLogoId? id = null) =>
        BrandLogo.Create(tenant ?? Ids.TenantA, PngBytes(), id);

    public static BrandLogo Svg(TenantId? tenant = null, BrandLogoId? id = null) =>
        BrandLogo.Create(tenant ?? Ids.TenantA, SvgBytes(), id);
}

public static class Brandings
{
    public static Branding Valid(BrandLogoId? light = null, BrandLogoId? dark = null, string english = "Brand", ArabicFont font = ArabicFont.Tajawal) =>
        Branding.Create(Names.Text(english, "علامة"), BrandPalette.Create(Colours.Primary, Colours.Accent), font, light, dark);

    public static Branding WithPrimary(string primaryHex) =>
        Branding.Create(Names.Text("Brand", "علامة"), BrandPalette.Create(BrandColour.Create(primaryHex), Colours.Accent), ArabicFont.Tajawal, null, null);
}
