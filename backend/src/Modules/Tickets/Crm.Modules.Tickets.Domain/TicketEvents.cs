using Crm.SharedKernel;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Tickets.Domain;

public sealed record TicketCreated(TicketId TicketId, DateTimeOffset OccurredOnUtc) : IDomainEvent;

public sealed record TicketStatusChanged(TicketId TicketId, TicketStatus From, TicketStatus To, DateTimeOffset OccurredOnUtc) : IDomainEvent;

public sealed record TicketAssignmentChanged(TicketId TicketId, Guid? From, Guid? To, DateTimeOffset OccurredOnUtc) : IDomainEvent;

public sealed record TicketTransferred(TicketId TicketId, DepartmentId From, DepartmentId To, DateTimeOffset OccurredOnUtc) : IDomainEvent;

public sealed record TicketPriorityChanged(TicketId TicketId, TicketPriority From, TicketPriority To, DateTimeOffset OccurredOnUtc) : IDomainEvent;

public sealed record TicketCategoryChanged(TicketId TicketId, CategoryId? From, CategoryId To, DateTimeOffset OccurredOnUtc) : IDomainEvent;
