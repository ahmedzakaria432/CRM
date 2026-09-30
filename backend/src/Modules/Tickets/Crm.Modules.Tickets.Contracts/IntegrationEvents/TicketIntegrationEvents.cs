using Crm.SharedKernel;

namespace Crm.Modules.Tickets.Contracts.IntegrationEvents;

/// <summary>Published by CreateTicketCommandHandler. Primitives only; append-only.</summary>
public sealed record TicketCreatedIntegrationEvent(
    Guid Id, DateTimeOffset OccurredOnUtc, Guid TicketId, string Number, Guid CustomerId, Guid? ContactId, string Channel,
    Guid? CategoryId, string Priority, string Status, Guid? AssignedAgentId, Guid ActorId, Guid TenantId, Guid BranchId,
    Guid DepartmentId) : IntegrationEvent(Id, OccurredOnUtc);

/// <summary>Published by TransitionTicketCommandHandler. Primitives only; append-only.</summary>
public sealed record TicketStatusChangedIntegrationEvent(
    Guid Id, DateTimeOffset OccurredOnUtc, Guid TicketId, string Number, string FromStatus, string ToStatus, Guid ActorId,
    Guid TenantId, Guid BranchId, Guid DepartmentId) : IntegrationEvent(Id, OccurredOnUtc);

/// <summary>Published by the Assign, Claim (AssignedAgentId = the actor) and Unassign (AssignedAgentId = null) handlers.
/// Primitives only; append-only.</summary>
public sealed record TicketAssignedIntegrationEvent(
    Guid Id, DateTimeOffset OccurredOnUtc, Guid TicketId, string Number, Guid? PreviousAgentId, Guid? AssignedAgentId,
    Guid ActorId, Guid TenantId, Guid BranchId, Guid DepartmentId) : IntegrationEvent(Id, OccurredOnUtc);

/// <summary>Published by TransferTicketCommandHandler. Primitives only; append-only.</summary>
public sealed record TicketTransferredIntegrationEvent(
    Guid Id, DateTimeOffset OccurredOnUtc, Guid TicketId, string Number, Guid FromBranchId, Guid FromDepartmentId,
    Guid ToBranchId, Guid ToDepartmentId, Guid? PreviousAgentId, Guid ActorId, Guid TenantId) : IntegrationEvent(Id, OccurredOnUtc);

/// <summary>Published by ChangeTicketPriorityCommandHandler. Primitives only; append-only.</summary>
public sealed record TicketPriorityChangedIntegrationEvent(
    Guid Id, DateTimeOffset OccurredOnUtc, Guid TicketId, string Number, string FromPriority, string ToPriority, Guid ActorId,
    Guid TenantId, Guid BranchId, Guid DepartmentId) : IntegrationEvent(Id, OccurredOnUtc);

/// <summary>Published by ChangeTicketCategoryCommandHandler. Primitives only; append-only.</summary>
public sealed record TicketCategoryChangedIntegrationEvent(
    Guid Id, DateTimeOffset OccurredOnUtc, Guid TicketId, string Number, Guid? FromCategoryId, Guid ToCategoryId, Guid ActorId,
    Guid TenantId, Guid BranchId, Guid DepartmentId) : IntegrationEvent(Id, OccurredOnUtc);
