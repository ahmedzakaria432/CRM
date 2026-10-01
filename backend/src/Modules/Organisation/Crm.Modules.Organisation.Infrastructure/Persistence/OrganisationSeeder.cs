using System.Text;
using Crm.BuildingBlocks.Infrastructure.Modules;
using Crm.BuildingBlocks.Infrastructure.Persistence;
using Crm.Modules.Organisation.Contracts.DevSeed;
using Crm.Modules.Organisation.Domain;
using Crm.SharedKernel.Scoping;
using Microsoft.EntityFrameworkCore;

namespace Crm.Modules.Organisation.Infrastructure.Persistence;

/// <summary>Development seed: one organisation, two branches, three departments, and memberships for the four
/// well-known user ids Story 07's user seed reuses. Idempotent per row - keyed on the well-known ids, never on
/// a freshly generated Guid.</summary>
public sealed class OrganisationSeeder(OrganisationDbContext context) : IModuleSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = new TenantId(OrganisationDevSeed.TenantId);

        if (!await context.Organisations.AnyAsync(o => o.Id == tenantId, cancellationToken))
        {
            context.Organisations.Add(Domain.Organisation.Create(
                OrganisationCode.Create("DEMO"),
                LocalizedText.Create("المنشأة التجريبية", "Demo Organisation"),
                tenantId));
            await context.SaveChangesAsync(cancellationToken);
        }

        var riyadhBranchId = new BranchId(OrganisationDevSeed.RiyadhBranchId);
        if (!await context.Branches.AnyAsync(b => b.Id == riyadhBranchId, cancellationToken))
        {
            context.Branches.Add(Branch.Create(
                tenantId, OrganisationCode.Create("RYD"), LocalizedText.Create("الرياض", "Riyadh"), riyadhBranchId));
        }

        var jeddahBranchId = new BranchId(OrganisationDevSeed.JeddahBranchId);
        if (!await context.Branches.AnyAsync(b => b.Id == jeddahBranchId, cancellationToken))
        {
            context.Branches.Add(Branch.Create(
                tenantId, OrganisationCode.Create("JED"), LocalizedText.Create("جدة", "Jeddah"), jeddahBranchId));
        }

        await context.SaveChangesAsync(cancellationToken);

        Branch riyadh = await context.Branches.SingleAsync(b => b.Id == riyadhBranchId, cancellationToken);
        Branch jeddah = await context.Branches.SingleAsync(b => b.Id == jeddahBranchId, cancellationToken);

        var riyadhCareId = new DepartmentId(OrganisationDevSeed.RiyadhCareDepartmentId);
        if (!await context.Departments.AnyAsync(d => d.Id == riyadhCareId, cancellationToken))
        {
            context.Departments.Add(Department.Create(
                riyadh, OrganisationCode.Create("RYD-CARE"), LocalizedText.Create("خدمة العملاء - الرياض", "Customer Care - Riyadh"), riyadhCareId));
        }

        var riyadhBillingId = new DepartmentId(OrganisationDevSeed.RiyadhBillingDepartmentId);
        if (!await context.Departments.AnyAsync(d => d.Id == riyadhBillingId, cancellationToken))
        {
            context.Departments.Add(Department.Create(
                riyadh, OrganisationCode.Create("RYD-BILL"), LocalizedText.Create("الفوترة - الرياض", "Billing - Riyadh"), riyadhBillingId));
        }

        var jeddahCareId = new DepartmentId(OrganisationDevSeed.JeddahCareDepartmentId);
        if (!await context.Departments.AnyAsync(d => d.Id == jeddahCareId, cancellationToken))
        {
            context.Departments.Add(Department.Create(
                jeddah, OrganisationCode.Create("JED-CARE"), LocalizedText.Create("خدمة العملاء - جدة", "Customer Care - Jeddah"), jeddahCareId));
        }

        await context.SaveChangesAsync(cancellationToken);

        Department riyadhCare = await context.Departments.SingleAsync(d => d.Id == riyadhCareId, cancellationToken);
        Department riyadhBilling = await context.Departments.SingleAsync(d => d.Id == riyadhBillingId, cancellationToken);
        Department jeddahCare = await context.Departments.SingleAsync(d => d.Id == jeddahCareId, cancellationToken);

        await EnsureMembershipAsync(riyadhCare, OrganisationDevSeed.AdminUserId, SystemRole.Administrator, OrganisationDevSeed.AdminCareMembershipId, cancellationToken);
        await EnsureMembershipAsync(riyadhBilling, OrganisationDevSeed.AdminUserId, SystemRole.Administrator, OrganisationDevSeed.AdminBillingMembershipId, cancellationToken);
        await EnsureMembershipAsync(jeddahCare, OrganisationDevSeed.AdminUserId, SystemRole.Administrator, OrganisationDevSeed.AdminJeddahCareMembershipId, cancellationToken);
        await EnsureMembershipAsync(riyadhCare, OrganisationDevSeed.SupervisorUserId, SystemRole.Supervisor, OrganisationDevSeed.SupervisorCareMembershipId, cancellationToken);
        await EnsureMembershipAsync(riyadhBilling, OrganisationDevSeed.SupervisorUserId, SystemRole.Agent, OrganisationDevSeed.SupervisorBillingMembershipId, cancellationToken);
        await EnsureMembershipAsync(riyadhCare, OrganisationDevSeed.AgentAUserId, SystemRole.Agent, OrganisationDevSeed.AgentACareMembershipId, cancellationToken);
        await EnsureMembershipAsync(jeddahCare, OrganisationDevSeed.AgentBUserId, SystemRole.Agent, OrganisationDevSeed.AgentBJeddahCareMembershipId, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        await EnsureDemoBrandingAsync(tenantId, cancellationToken);
    }

    private const string LightLogoSvg =
        "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 160 40\" width=\"160\" height=\"40\">" +
        "<rect x=\"2\" y=\"6\" width=\"28\" height=\"28\" rx=\"7\" fill=\"#0E6655\"/>" +
        "<path d=\"M10 26 L16 12 L22 26 Z\" fill=\"#FFFFFF\"/>" +
        "<text x=\"40\" y=\"28\" font-family=\"sans-serif\" font-size=\"22\" font-weight=\"700\" fill=\"#0E6655\">DEMO</text></svg>";

    private const string DarkLogoSvg =
        "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 160 40\" width=\"160\" height=\"40\">" +
        "<rect x=\"2\" y=\"6\" width=\"28\" height=\"28\" rx=\"7\" fill=\"#B7791F\"/>" +
        "<path d=\"M10 26 L16 12 L22 26 Z\" fill=\"#FFFFFF\"/>" +
        "<text x=\"40\" y=\"28\" font-family=\"sans-serif\" font-size=\"22\" font-weight=\"700\" fill=\"#FFFFFF\">DEMO</text></svg>";

    // Story 58 (D11): the MVP demo wants a branded portal. Applied only while the DEMO organisation was never
    // branded, so an administrator's revert to the defaults is never overwritten on restart.
    private async Task EnsureDemoBrandingAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        foreach ((Guid id, string svg) in new[] { (OrganisationDevSeed.DemoLightLogoId, LightLogoSvg), (OrganisationDevSeed.DemoDarkLogoId, DarkLogoSvg) })
        {
            var logoId = new BrandLogoId(id);
            if (!await context.BrandLogos.AnyAsync(l => l.Id == logoId, cancellationToken))
            {
                context.BrandLogos.Add(BrandLogo.Create(tenantId, Encoding.UTF8.GetBytes(svg), logoId));
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        Domain.Organisation organisation = await context.Organisations.SingleAsync(o => o.Id == tenantId, cancellationToken);
        if (organisation.Branding is null && !organisation.CanRevertBranding)
        {
            organisation.ChangeBranding(Branding.Create(
                LocalizedText.Create("مركز الدعم التجريبي", "Demo Support Centre"),
                BrandPalette.Create(BrandColour.Create("#0E6655"), BrandColour.Create("#B7791F")),
                ArabicFont.Tajawal,
                new BrandLogoId(OrganisationDevSeed.DemoLightLogoId),
                new BrandLogoId(OrganisationDevSeed.DemoDarkLogoId)));
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    // A removed seed membership stays soft-deleted at its well-known id (the filtered unique index would allow
    // a fresh insert to clash with it) - checking with the soft-delete filter lifted keeps this idempotent
    // without resurrecting a deliberately removed row.
    private async Task EnsureMembershipAsync(Department department, Guid userId, SystemRole role, Guid membershipId, CancellationToken cancellationToken)
    {
        var id = new MembershipId(membershipId);
        bool exists = await context.Memberships
            .IgnoreQueryFilters([ModuleDbContext.SoftDeleteFilterName])
            .AnyAsync(m => m.Id == id, cancellationToken);

        if (!exists)
        {
            context.Memberships.Add(Membership.Create(department, userId, role, id));
        }
    }
}
