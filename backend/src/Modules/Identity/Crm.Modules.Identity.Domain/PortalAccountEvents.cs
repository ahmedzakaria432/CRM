using Crm.SharedKernel;

namespace Crm.Modules.Identity.Domain;

/// <summary>KeepSessionId is the caller's own session on a self-service change; null when an agent sets the password.</summary>
public sealed record PortalPasswordChanged(PortalAccountId AccountId, Guid? KeepSessionId, DateTimeOffset OccurredOnUtc) : IDomainEvent;

public sealed record PortalSessionStarted(PortalAccountId AccountId, PortalSessionId SessionId, DateTimeOffset OccurredOnUtc) : IDomainEvent;
