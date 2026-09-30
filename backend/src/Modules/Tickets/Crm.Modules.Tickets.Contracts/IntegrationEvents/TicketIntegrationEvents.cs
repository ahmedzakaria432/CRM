using Crm.SharedKernel;

namespace Crm.Modules.Tickets.Contracts.IntegrationEvents;

/// <summary>Published by CreateTicketCommandHandler. Primitives only; append-only.</summary>
public sealed record TicketCreatedIntegrationEvent(
    Guid Id, DateTimeOffset OccurredOnUtc, Guid TicketId, string Number, Guid CustomerId, Guid? ContactId, string Channel,
    Guid? CategoryId, string Priority, string Status, Guid? AssignedAgentId, Guid ActorId, Guid TenantId, Guid BranchId,
    Guid DepartmentId) : IntegrationEvent(Id, OccurredOnUtc);

/// <summary>Published by TransitionTicketCommandHandler, and by the reply handlers when a reply reopens the ticket
/// (Story 18). Primitives only; append-only.</summary>
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

/// <summary>Published by ReplyAsAgentCommandHandler and ReceiveCustomerReplyCommandHandler (Story 18) for every PUBLIC
/// message - never for an internal note. Direction is "Inbound" or "Outbound"; AuthorId is the agent (outbound) or a
/// contact id / null for the customer (inbound). No body on purpose: subscribers that need it read it through a
/// Contracts API. Primitives only; append-only.</summary>
public sealed record TicketMessageAddedIntegrationEvent(
    Guid Id, DateTimeOffset OccurredOnUtc, Guid TicketId, string Number, Guid MessageId, string Direction, Guid? AuthorId,
    Guid CustomerId, Guid? ContactId, bool IsFirstResponse, Guid ActorId, Guid TenantId, Guid BranchId,
    Guid DepartmentId) : IntegrationEvent(Id, OccurredOnUtc);
