using Crm.SharedKernel;

namespace Crm.Modules.Customers.Contracts.IntegrationEvents;

/// <summary>Published by UpdateCustomer, DeactivateCustomer and ReactivateCustomer. Primitives only; append-only.</summary>
public sealed record CustomerUpdatedIntegrationEvent(
    Guid Id, DateTimeOffset OccurredOnUtc, Guid CustomerId, string DisplayNameAr, string DisplayNameEn, string Type,
    string Status) : IntegrationEvent(Id, OccurredOnUtc);
