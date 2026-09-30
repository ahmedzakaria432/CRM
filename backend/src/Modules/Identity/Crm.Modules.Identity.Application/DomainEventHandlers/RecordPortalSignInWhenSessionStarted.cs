using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.Modules.Identity.Domain;
using MediatR;

namespace Crm.Modules.Identity.Application.DomainEventHandlers;

/// <summary>Runs after the portal sign-in commits: one transaction, one aggregate - the session is created by the
/// command, the last-sign-in fact of the account is recorded here (Story 47).</summary>
public sealed class RecordPortalSignInWhenSessionStarted(IRepository<PortalAccount, PortalAccountId> accounts, IIdentityUnitOfWork unitOfWork)
    : INotificationHandler<DomainEventNotification<PortalSessionStarted>>
{
    public async Task Handle(DomainEventNotification<PortalSessionStarted> notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        PortalSessionStarted started = notification.DomainEvent;
        PortalAccount? account = await accounts.GetByIdAsync(started.AccountId, cancellationToken);

        if (account is null)
        {
            return;
        }

        account.RecordSuccessfulSignIn(started.OccurredOnUtc);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
