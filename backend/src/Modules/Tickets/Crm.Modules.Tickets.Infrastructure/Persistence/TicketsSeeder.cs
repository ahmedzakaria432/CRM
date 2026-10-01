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
        Category complaint = await EnsureCategoryAsync(
            TicketsDevSeed.RyCareComplaintCategoryId, tenant, riyadhCare, "شكوى", "Complaint", 30, true, false, cancellationToken);
        await EnsureCategoryAsync(
            TicketsDevSeed.RyCareInternalCategoryId, tenant, riyadhCare, "متابعة داخلية", "Internal follow-up", 40, false, false, cancellationToken);
        await EnsureCategoryAsync(
            TicketsDevSeed.RyCareLegacyCategoryId, tenant, riyadhCare, "تصنيف قديم", "Legacy", 90, false, true, cancellationToken);
        Category invoice = await EnsureCategoryAsync(
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

        // Story 59: one ticket per status/priority/department case; the lambda applies the aggregate methods in legal order.
        await EnsureTicketAsync(
            TicketsDevSeed.AlNoorOpenTicketId, "Clinic booking screen fails to load",
            "The booking screen shows a blank page for the front desk since this morning.",
            CustomersDevSeed.AlNoorClinicsCustomerId, CustomersDevSeed.AlNoorMonaContactId, TicketChannel.Agent, technical, TicketPriority.High,
            tenant, riyadhBranch, riyadhCare,
            (t, by, at) => { t.Open(by, at); t.AssignTo(OrganisationDevSeed.AgentAUserId, by, at); }, cancellationToken);

        await EnsureTicketAsync(
            TicketsDevSeed.AlNoorPendingTicketId, "نحتاج إلى تقرير الاستخدام الشهري",
            "نرجو تزويدنا بتقرير الاستخدام لشهر سبتمبر لجميع الفروع.",
            CustomersDevSeed.AlNoorClinicsCustomerId, CustomersDevSeed.AlNoorMonaContactId, TicketChannel.Portal, general, TicketPriority.Normal,
            tenant, riyadhBranch, riyadhCare,
            (t, by, at) => { t.Open(by, at); t.AssignTo(OrganisationDevSeed.AgentAUserId, by, at); t.ReplyAsAgent("Could you confirm which branches the report should cover?", OrganisationDevSeed.AgentAUserId, at); t.AwaitCustomer(by, at); }, cancellationToken);

        await EnsureTicketAsync(
            TicketsDevSeed.AlNoorYousefNewTicketId, "Change the opening hours on our profile",
            "Please update the clinic opening hours to 8am-10pm.",
            CustomersDevSeed.AlNoorClinicsCustomerId, CustomersDevSeed.AlNoorYousefContactId, TicketChannel.Portal, general, TicketPriority.Low,
            tenant, riyadhBranch, riyadhCare,
            (_, _, _) => { }, cancellationToken);

        await EnsureTicketAsync(
            TicketsDevSeed.AlNoorYousefResolvedTicketId, "Complaint about a delayed callback",
            "We were promised a callback within a day and are still waiting.",
            CustomersDevSeed.AlNoorClinicsCustomerId, CustomersDevSeed.AlNoorYousefContactId, TicketChannel.Portal, complaint, TicketPriority.Normal,
            tenant, riyadhBranch, riyadhCare,
            (t, by, at) => { t.Open(by, at); t.AssignTo(OrganisationDevSeed.SupervisorBUserId, by, at); t.Resolve(by, at); }, cancellationToken);

        await EnsureTicketAsync(
            TicketsDevSeed.AlNoorClosedTicketId, "Question about the service agreement",
            "Which services are covered under our current agreement?",
            CustomersDevSeed.AlNoorClinicsCustomerId, CustomersDevSeed.AlNoorMonaContactId, TicketChannel.Agent, general, TicketPriority.Low,
            tenant, riyadhBranch, riyadhCare,
            (t, by, at) => { t.Open(by, at); t.AssignTo(OrganisationDevSeed.AgentAUserId, by, at); t.Resolve(by, at); t.Close(by, at); }, cancellationToken);

        await EnsureTicketAsync(
            TicketsDevSeed.AlNoorOnHoldTicketId, "تعذّر استلام رسائل التنبيه",
            "لا تصلنا رسائل التنبيه الخاصة بالمواعيد منذ أسبوع.",
            CustomersDevSeed.AlNoorClinicsCustomerId, CustomersDevSeed.AlNoorMonaContactId, TicketChannel.Agent, technical, TicketPriority.Urgent,
            tenant, riyadhBranch, riyadhCare,
            (t, by, at) => { t.Open(by, at); t.AssignTo(OrganisationDevSeed.SupervisorBUserId, by, at); t.PutOnHold(by, at); }, cancellationToken);

        await EnsureTicketAsync(
            TicketsDevSeed.AlNoorCancelledTicketId, "Duplicate request - please ignore",
            "Submitted twice by mistake.",
            CustomersDevSeed.AlNoorClinicsCustomerId, CustomersDevSeed.AlNoorMonaContactId, TicketChannel.Agent, general, TicketPriority.Normal,
            tenant, riyadhBranch, riyadhCare,
            (t, by, at) => { t.Cancel(by, at); }, cancellationToken);

        await EnsureTicketAsync(
            TicketsDevSeed.GulfRetailNewTicketId, "Invoice INV-2041 is missing a line item",
            "The September invoice does not list the delivery fee.",
            CustomersDevSeed.GulfRetailCustomerId, CustomersDevSeed.GulfRetailNasserContactId, TicketChannel.Agent, invoice, TicketPriority.Normal,
            tenant, riyadhBranch, riyadhBilling,
            (_, _, _) => { }, cancellationToken);

        await EnsureTicketAsync(
            TicketsDevSeed.GulfRetailOpenTicketId, "Duplicate charge on the October invoice",
            "We were charged twice for the same shipment.",
            CustomersDevSeed.GulfRetailCustomerId, CustomersDevSeed.GulfRetailNasserContactId, TicketChannel.Agent, invoice, TicketPriority.High,
            tenant, riyadhBranch, riyadhBilling,
            (t, by, at) => { t.Open(by, at); t.AssignTo(OrganisationDevSeed.SupervisorUserId, by, at); }, cancellationToken);

        await EnsureTicketAsync(
            TicketsDevSeed.RedSeaOpenTicketId, "طلب تعديل بيانات الشحن",
            "نرغب في تعديل عنوان الشحن الافتراضي لحسابنا.",
            CustomersDevSeed.RedSeaTradingCustomerId, CustomersDevSeed.RedSeaFahadContactId, TicketChannel.Agent, jeddahGeneral, TicketPriority.Normal,
            tenant, jeddahBranch, jeddahCare,
            (t, by, at) => { t.Open(by, at); t.AssignTo(OrganisationDevSeed.AgentBUserId, by, at); }, cancellationToken);

        await EnsureTicketAsync(
            TicketsDevSeed.RedSeaResolvedTicketId, "Statement of account request",
            "Please send the statement of account for Q3.",
            CustomersDevSeed.RedSeaTradingCustomerId, CustomersDevSeed.RedSeaFahadContactId, TicketChannel.Agent, jeddahGeneral, TicketPriority.Low,
            tenant, jeddahBranch, jeddahCare,
            (t, by, at) => { t.Open(by, at); t.AssignTo(OrganisationDevSeed.AgentBUserId, by, at); t.Resolve(by, at); }, cancellationToken);

        await EnsureTicketAsync(
            TicketsDevSeed.RedSeaHighNewTicketId, "Shipment held at customs",
            "Our shipment has been held at customs for three days.",
            CustomersDevSeed.RedSeaTradingCustomerId, CustomersDevSeed.RedSeaFahadContactId, TicketChannel.Agent, jeddahGeneral, TicketPriority.High,
            tenant, jeddahBranch, jeddahCare,
            (_, _, _) => { }, cancellationToken);
    }

    private async Task EnsureTicketAsync(
        Guid id, string subject, string description, Guid customerId, Guid contactId, TicketChannel channel, Category category,
        TicketPriority priority, TenantId tenant, BranchId branch, DepartmentId department, Action<Ticket, Guid, DateTimeOffset> progress,
        CancellationToken cancellationToken)
    {
        if (await TicketExistsAsync(id, cancellationToken))
        {
            return;
        }

        Guid admin = OrganisationDevSeed.AdminUserId;
        DateTimeOffset now = dateTime.UtcNow;
        Ticket ticket = Ticket.Create(
            await numbers.NextAsync(cancellationToken), subject, description, customerId, contactId, channel, category, priority,
            tenant, branch, department, admin, now, new TicketId(id));
        progress(ticket, admin, now);
        await SaveAsync(ticket, cancellationToken);
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
