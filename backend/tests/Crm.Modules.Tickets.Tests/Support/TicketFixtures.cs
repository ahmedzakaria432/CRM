using System.Reflection;
using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Customers.Contracts;
using Crm.Modules.Identity.Contracts;
using Crm.Modules.Organisation.Contracts;
using Crm.Modules.Tickets.Domain;
using Crm.SharedKernel;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Tickets.Tests.Support;

/// <summary>Builders over the public domain API only. A class named Tickets would collide with the
/// Crm.Modules.Tickets namespace, hence the name.</summary>
public static class TicketFixtures
{
    public static TicketNumber Number(long sequence = 1) => TicketNumber.FromSequence(sequence);

    public static LocalizedText Name(string ar = "استفسار عام", string en = "General enquiry") => LocalizedText.Create(ar, en);

    public static Category NewCategory(
        TenantId? tenant = null, DepartmentId? department = null, string ar = "استفسار عام", string en = "General enquiry",
        int order = 10, bool visible = true, bool active = true, CategoryId? id = null)
    {
        Category category = Category.Create(
            tenant ?? Ids.TenantA, department ?? Ids.DepartmentA1, Name(ar, en), order, visible, id);
        if (!active)
        {
            category.Deactivate();
        }

        return category;
    }

    /// <summary>A New ticket of tenant A / branch A1 / department A1 for customer 1, created by the default actor.</summary>
    public static Ticket Create(
        long number = 1, string subject = "Printer is down", string description = "The office printer shows an error.",
        Guid? customer = null, Guid? contact = null, TicketChannel channel = TicketChannel.Agent, Category? category = null,
        TicketPriority priority = TicketPriority.Normal, TenantId? tenant = null, BranchId? branch = null,
        DepartmentId? department = null, Guid? actor = null, DateTimeOffset? at = null, TicketId? id = null) =>
        Ticket.Create(
            Number(number), subject, description, customer ?? Ids.Customer1, contact, channel, category, priority,
            tenant ?? Ids.TenantA, branch ?? Ids.BranchA1, department ?? Ids.DepartmentA1, actor ?? Ids.Actor, at ?? Clock.Now, id);

    /// <summary>Walks the legal path to the status.</summary>
    public static Ticket InStatus(TicketStatus status, long number = 1, Guid? contact = null, Guid? customer = null, TicketId? id = null)
    {
        Ticket ticket = Create(number, contact: contact, customer: customer, id: id);
        switch (status)
        {
            case TicketStatus.New:
                break;
            case TicketStatus.Open:
                ticket.Open(Ids.Actor, Clock.Now);
                break;
            case TicketStatus.PendingCustomer:
                ticket.Open(Ids.Actor, Clock.Now);
                ticket.AwaitCustomer(Ids.Actor, Clock.Now);
                break;
            case TicketStatus.OnHold:
                ticket.Open(Ids.Actor, Clock.Now);
                ticket.PutOnHold(Ids.Actor, Clock.Now);
                break;
            case TicketStatus.Resolved:
                ticket.Resolve(Ids.Actor, Clock.Now);
                break;
            case TicketStatus.Closed:
                ticket.Resolve(Ids.Actor, Clock.Now);
                ticket.Close(Ids.Actor, Clock.Now);
                break;
            case TicketStatus.Cancelled:
                ticket.Cancel(Ids.Actor, Clock.Now);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }

        ticket.ClearDomainEvents();
        return ticket;
    }

    public static Ticket Assigned(this Ticket ticket, Guid agent)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        ticket.AssignTo(agent, Ids.Actor, Clock.Now);
        ticket.ClearDomainEvents();
        return ticket;
    }

    /// <summary>Sets the audit timestamps the interceptor would write, for tickets that never pass through a context.</summary>
    public static Ticket Stamp(this Ticket ticket, DateTimeOffset? created, DateTimeOffset? modified = null, string? createdBy = null)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        SetAudit(ticket, "CreatedAtUtc", created);
        SetAudit(ticket, "ModifiedAtUtc", modified);
        SetAudit(ticket, "CreatedBy", createdBy);
        return ticket;
    }

    private static void SetAudit(Ticket ticket, string name, object? value)
    {
        PropertyInfo property = typeof(AuditableEntity<TicketId>).GetProperty(name) ?? throw new InvalidOperationException(name);
        property.SetValue(ticket, value);
    }

    public static CustomerSummary Summary(
        Guid? id = null, bool active = true, IReadOnlyList<CustomerContactSummary>? contacts = null, TenantId? tenant = null,
        BranchId? branch = null, DepartmentId? department = null) =>
        new(id ?? Ids.Customer1, "C-000001", "أحمد", "Ahmed", "Individual", active, "en", (tenant ?? Ids.TenantA).Value,
            (branch ?? Ids.BranchA1).Value, (department ?? Ids.DepartmentA1).Value,
            contacts ?? [new CustomerContactSummary(Ids.Contact1, "Contact One", "one@example.com", null, true, false)]);

    public static StaffUserSummary Staff(Guid id, string name = "Agent One", bool active = true) => new(id, name, active);

    public static DepartmentReference Department(
        DepartmentId? id = null, TenantId? tenant = null, BranchId? branch = null, string code = "A1", string nameAr = "قسم", string nameEn = "Department", bool active = true) =>
        new((id ?? Ids.DepartmentA1).Value, (tenant ?? Ids.TenantA).Value, (branch ?? Ids.BranchA1).Value, code, nameAr, nameEn, active);

    public static OrganisationScope AgentOf(params (DepartmentId Department, BranchId Branch)[] memberships) =>
        Scopes.Agent(Ids.TenantA, [.. memberships.Select(m => Scopes.Member(m.Department, m.Branch))]);
}
