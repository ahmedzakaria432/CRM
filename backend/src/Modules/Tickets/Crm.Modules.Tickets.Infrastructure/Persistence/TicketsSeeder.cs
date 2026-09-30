using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Infrastructure.Modules;
using Crm.BuildingBlocks.Infrastructure.Persistence;
using Crm.Modules.Customers.Contracts.DevSeed;
using Crm.Modules.Organisation.Contracts.DevSeed;
using Crm.Modules.Tickets.Application.Abstractions;
using Crm.Modules.Tickets.Contracts.DevSeed;
using Crm.Modules.Tickets.Domain;
using Crm.SharedKernel.Scoping;
using Microsoft.EntityFrameworkCore;

namespace Crm.Modules.Tickets.Infrastructure.Persistence;

public sealed class TicketsSeeder(TicketsDbContext context, ITicketNumberGenerator numbers, IDateTime dateTime) : IModuleSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var tenant = new TenantId(OrganisationDevSeed.TenantId);
        var riyadhBranch = new BranchId(OrganisationDevSeed.RiyadhBranchId);
        var riyadhCare = new DepartmentId(OrganisationDevSeed.RiyadhCareDepartmentId);
        var riyadhBilling = new DepartmentId(OrganisationDevSeed.RiyadhBillingDepartmentId);
        var jeddahBranch = new BranchId(OrganisationDevSeed.JeddahBranchId);
        var jeddahCare = new DepartmentId(OrganisationDevSeed.JeddahCareDepartmentId);
        Guid admin = OrganisationDevSeed.AdminUserId;

        Category general = await EnsureCategoryAsync(
            TicketsDevSeed.RyCareGeneralCategoryId, tenant, riyadhCare, "استفسار عام", "General enquiry", 10, true, false, cancellationToken);
        Category technical = await EnsureCategoryAsync(
            TicketsDevSeed.RyCareTechnicalCategoryId, tenant, riyadhCare, "مشكلة تقنية", "Technical issue", 20, true, false, cancellationToken);
        await EnsureCategoryAsync(
            TicketsDevSeed.RyCareComplaintCategoryId, tenant, riyadhCare, "شكوى", "Complaint", 30, true, false, cancellationToken);
        await EnsureCategoryAsync(
            TicketsDevSeed.RyCareInternalCategoryId, tenant, riyadhCare, "متابعة داخلية", "Internal follow-up", 40, false, false, cancellationToken);
        await EnsureCategoryAsync(
            TicketsDevSeed.RyCareLegacyCategoryId, tenant, riyadhCare, "تصنيف قديم", "Legacy", 90, false, true, cancellationToken);
        await EnsureCategoryAsync(
            TicketsDevSeed.RyBillInvoiceCategoryId, tenant, riyadhBilling, "استفسار عن فاتورة", "Invoice question", 10, true, false, cancellationToken);
        Category jeddahGeneral = await EnsureCategoryAsync(
            TicketsDevSeed.JeCareGeneralCategoryId, tenant, jeddahCare, "استفسار عام", "General enquiry", 10, true, false, cancellationToken);

        if (!await TicketExistsAsync(TicketsDevSeed.AhmedNewTicketId, cancellationToken))
        {
            Ticket ahmed = Ticket.Create(
                await numbers.NextAsync(cancellationToken), "لا أستطيع تسجيل الدخول إلى البوابة",
                "يظهر لي خطأ عند محاولة تسجيل الدخول إلى بوابة العملاء.", CustomersDevSeed.AhmedCustomerId,
                CustomersDevSeed.AhmedContactId, TicketChannel.Agent, general, TicketPriority.Normal, tenant, riyadhBranch, riyadhCare,
                admin, dateTime.UtcNow, new TicketId(TicketsDevSeed.AhmedNewTicketId));

            await SaveAsync(ahmed, cancellationToken);
        }

        if (!await TicketExistsAsync(TicketsDevSeed.NajdOpenTicketId, cancellationToken))
        {
            Ticket najd = Ticket.Create(
                await numbers.NextAsync(cancellationToken), "Delivery tracking shows the wrong status",
                "The tracking page still shows the shipment as pending although it was delivered yesterday.",
                CustomersDevSeed.NajdLogisticsCustomerId, CustomersDevSeed.NajdSaraContactId, TicketChannel.Agent, technical,
                TicketPriority.High, tenant, riyadhBranch, riyadhCare, admin, dateTime.UtcNow, new TicketId(TicketsDevSeed.NajdOpenTicketId));

            najd.Open(admin, dateTime.UtcNow);
            najd.AssignTo(OrganisationDevSeed.AgentAUserId, admin, dateTime.UtcNow);

            await SaveAsync(najd, cancellationToken);
        }

        if (!await TicketExistsAsync(TicketsDevSeed.RedSeaNewTicketId, cancellationToken))
        {
            Ticket redSea = Ticket.Create(
                await numbers.NextAsync(cancellationToken), "Invoice total does not match the quote",
                "The invoice total is higher than the quotation we approved.", CustomersDevSeed.RedSeaTradingCustomerId,
                CustomersDevSeed.RedSeaFahadContactId, TicketChannel.Agent, jeddahGeneral, TicketPriority.Urgent, tenant, jeddahBranch,
                jeddahCare, admin, dateTime.UtcNow, new TicketId(TicketsDevSeed.RedSeaNewTicketId));

            await SaveAsync(redSea, cancellationToken);
        }
    }

    // Keyed on a stable well-known id, never a freshly generated Guid. Both named filters are lifted: the seeder
    // must see soft-deleted rows too, and runs with no HttpContext (OrganisationScope.Unrestricted).
    private async Task<Category> EnsureCategoryAsync(
        Guid id, TenantId tenant, DepartmentId department, string nameAr, string nameEn, int order, bool customerVisible,
        bool inactive, CancellationToken cancellationToken)
    {
        var categoryId = new CategoryId(id);
        Category? existing = await context.Categories
            .IgnoreQueryFilters([ModuleDbContext.SoftDeleteFilterName, ModuleDbContext.ScopeFilterName])
            .FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        Category category = Category.Create(tenant, department, LocalizedText.Create(nameAr, nameEn), order, customerVisible, categoryId);
        if (inactive)
        {
            category.Deactivate();
        }

        context.Categories.Add(category);
        await context.SaveChangesAsync(cancellationToken);
        return category;
    }

    private Task<bool> TicketExistsAsync(Guid id, CancellationToken cancellationToken)
    {
        var ticketId = new TicketId(id);
        return context.Tickets
            .IgnoreQueryFilters([ModuleDbContext.SoftDeleteFilterName, ModuleDbContext.ScopeFilterName])
            .AnyAsync(t => t.Id == ticketId, cancellationToken);
    }

    private async Task SaveAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync(cancellationToken);
    }
}
