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

public sealed record TicketListItemDto(
    Guid Id, string Number, string Subject, string Status, string Priority, string Channel, Guid CustomerId, Guid? ContactId,
    TicketCategoryRefDto? Category, Guid? AssignedAgentId, string? AssignedAgentName, Guid BranchId, Guid DepartmentId,
    DateTimeOffset? CreatedAtUtc, DateTimeOffset? ModifiedAtUtc);

public sealed record TicketQueueCountsDto(int Mine, int Unassigned, int All);
