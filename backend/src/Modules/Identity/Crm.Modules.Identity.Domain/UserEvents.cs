using Crm.SharedKernel;

namespace Crm.Modules.Identity.Domain;

public sealed record UserDeactivated(UserId UserId, DateTimeOffset OccurredOnUtc) : IDomainEvent;

/// <summary>KeepSessionId is the caller's own session on a self-service change; null on an admin reset.</summary>
public sealed record UserPasswordChanged(UserId UserId, Guid? KeepSessionId, DateTimeOffset OccurredOnUtc) : IDomainEvent;

public sealed record SessionStarted(UserId UserId, RefreshTokenFamilyId SessionId, DateTimeOffset OccurredOnUtc) : IDomainEvent;
