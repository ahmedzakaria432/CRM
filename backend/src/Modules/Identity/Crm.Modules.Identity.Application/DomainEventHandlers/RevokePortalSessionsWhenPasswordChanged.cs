using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.Modules.Identity.Application.Specifications;
using Crm.Modules.Identity.Domain;
using MediatR;

namespace Crm.Modules.Identity.Application.DomainEventHandlers;

/// <summary>A self-service change keeps the caller own session; an agent setting the password keeps none (Story 47).</summary>
public sealed class RevokePortalSessionsWhenPasswordChanged(
    IRepository<PortalSession, PortalSessionId> sessions,
    IDateTime dateTime,
    IIdentityUnitOfWork unitOfWork)
    : INotificationHandler<DomainEventNotification<PortalPasswordChanged>>
{
    public async Task Handle(DomainEventNotification<PortalPasswordChanged> notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        PortalPasswordChanged changed = notification.DomainEvent;

        IReadOnlyList<PortalSession> active = await sessions.ListAsync(
            new ActivePortalSessionsOfAccountSpecification(changed.AccountId), cancellationToken);

        SessionRevocationReason reason = changed.KeepSessionId is null
            ? SessionRevocationReason.PasswordReset
            : SessionRevocationReason.PasswordChanged;

        DateTimeOffset now = dateTime.UtcNow;

        foreach (PortalSession session in active.Where(s => s.Id.Value != changed.KeepSessionId))
        {
            session.Revoke(reason, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
