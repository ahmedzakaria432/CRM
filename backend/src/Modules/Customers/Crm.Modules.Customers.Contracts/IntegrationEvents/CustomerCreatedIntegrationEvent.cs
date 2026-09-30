using Crm.SharedKernel;

namespace Crm.Modules.Customers.Contracts.IntegrationEvents;

/// <summary>Published by CreateCustomerCommandHandler. Primitives only; append-only (never rename or remove a member).</summary>
public sealed record CustomerCreatedIntegrationEvent(
    Guid Id, DateTimeOffset OccurredOnUtc, Guid CustomerId, string Number, string DisplayNameAr, string DisplayNameEn,
    string Type, Guid TenantId, Guid BranchId, Guid DepartmentId) : IntegrationEvent(Id, OccurredOnUtc);
