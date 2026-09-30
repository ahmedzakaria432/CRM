using Crm.Modules.Tickets.Domain;

namespace Crm.Modules.Tickets.Application;

// Primitives only: the one place in Application that touches domain types for mapping.
public sealed record TicketCategoryNameDto(string Ar, string En)
{
    public static TicketCategoryNameDto FromDomain(LocalizedText name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return new TicketCategoryNameDto(name.Arabic, name.English);
    }
}

/// <summary>The admin row and the picker item.</summary>
public sealed record TicketCategoryDto(
    Guid Id, Guid DepartmentId, TicketCategoryNameDto Name, int DisplayOrder, bool IsCustomerVisible, bool IsActive, byte[] RowVersion)
{
    public static TicketCategoryDto FromDomain(Category category)
    {
        ArgumentNullException.ThrowIfNull(category);
        return new TicketCategoryDto(
            category.Id.Value, category.DepartmentId.Value, TicketCategoryNameDto.FromDomain(category.Name), category.DisplayOrder,
            category.IsCustomerVisible, category.IsActive, category.RowVersion);
    }
}

/// <summary>What a ticket shows of its category - also when the category was deactivated later.</summary>
public sealed record TicketCategoryRefDto(Guid Id, TicketCategoryNameDto Name, bool IsActive);

public sealed record TicketPersonDto(Guid Id, string DisplayName);

public sealed record TicketActivityDto(Guid Id, string Kind, Guid ActorId, string? From, string? To, DateTimeOffset OccurredAtUtc);

public sealed record TicketDto(
    Guid Id, string Number, string Subject, string Description, string Status, string Priority, string Channel,
    Guid CustomerId, Guid? ContactId, TicketCategoryRefDto? Category, Guid? AssignedAgentId, string? AssignedAgentName,
    Guid TenantId, Guid BranchId, Guid DepartmentId,
    DateTimeOffset? CreatedAtUtc, string? CreatedBy, DateTimeOffset? ModifiedAtUtc, DateTimeOffset? FirstRespondedAtUtc,
    DateTimeOffset? ResolvedAtUtc, DateTimeOffset? ClosedAtUtc,
    IReadOnlyList<string> AllowedActions, IReadOnlyList<TicketActivityDto> Activities, IReadOnlyList<TicketPersonDto> People,
    byte[] RowVersion);

/// <summary>The customer a list row belongs to (Story 20). Null on the row when the caller cannot see the customer.</summary>
public sealed record TicketCustomerRefDto(Guid Id, string Number, string NameAr, string NameEn);

public sealed record TicketListItemDto(
    Guid Id, string Number, string Subject, string Status, string Priority, string Channel, Guid CustomerId, Guid? ContactId,
    TicketCategoryRefDto? Category, Guid? AssignedAgentId, string? AssignedAgentName, Guid BranchId, Guid DepartmentId,
    DateTimeOffset? CreatedAtUtc, DateTimeOffset? ModifiedAtUtc,
    TicketCustomerRefDto? Customer);

public sealed record TicketQueueCountsDto(int Mine, int Unassigned, int All);

/// <summary>A department named by a Transferred activity.</summary>
public sealed record TicketDepartmentRefDto(Guid Id, string Code, string NameAr, string NameEn);

/// <summary>An active staff member who can take tickets of the listed departments (Story 20 pickers).</summary>
public sealed record TicketAgentDto(Guid Id, string DisplayName, IReadOnlyList<Guid> DepartmentIds);

/// <summary>One thread entry. Type (Message | Note | Activity) and Visibility (Public | Internal | System) map 1:1.
/// Messages and notes carry Body (plain text - render as text, never as HTML); messages carry Direction
/// (Inbound | Outbound) and RecordedById; activities carry TemplateKey (the activity kind, e.g. "PriorityChanged")
/// and Parameters ("from", "to": machine values the UI resolves). AuthorId: agent id, contact id, or null for the
/// customer (inbound messages); the actor for activities.</summary>
public sealed record TicketThreadEntryDto(
    int Sequence, Guid Id, string Type, string Visibility, DateTimeOffset OccurredAtUtc, Guid? AuthorId,
    string? Direction, Guid? RecordedById, string? Body, string? TemplateKey, IReadOnlyDictionary<string, string?>? Parameters);

/// <summary>A page of the thread, oldest first. NextCursor: pass it as `after` for the next page; null = no more.
/// The lookups name every staff user, category and department the page's entries refer to.</summary>
public sealed record TicketThreadPageDto(
    IReadOnlyList<TicketThreadEntryDto> Items, int? NextCursor, IReadOnlyList<TicketPersonDto> People,
    IReadOnlyList<TicketCategoryRefDto> Categories, IReadOnlyList<TicketDepartmentRefDto> Departments);
