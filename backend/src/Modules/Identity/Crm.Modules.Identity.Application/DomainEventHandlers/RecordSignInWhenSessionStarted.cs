using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.Modules.Identity.Domain;
using MediatR;

namespace Crm.Modules.Identity.Application.DomainEventHandlers;

/// <summary>Runs after the sign-in commits: one transaction, one aggregate - the session (family) is created
/// by the command, the user's last-sign-in fact is recorded here.</summary>
public sealed class RecordSignInWhenSessionStarted(IRepository<User, UserId> users, IIdentityUnitOfWork unitOfWork)
    : INotificationHandler<DomainEventNotification<SessionStarted>>
{
    public async Task Handle(DomainEventNotification<SessionStarted> notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        SessionStarted started = notification.DomainEvent;
        User? user = await users.GetByIdAsync(started.UserId, cancellationToken);

        if (user is null)
        {
            return;
        }

        user.RecordSuccessfulSignIn(started.OccurredOnUtc);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
