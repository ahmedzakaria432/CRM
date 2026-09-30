using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Customers.Contracts;
using Crm.Modules.Identity.Application.Abstractions;
using Crm.Modules.Identity.Application.Specifications;
using Crm.Modules.Identity.Domain;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;
using MediatR;

namespace Crm.Modules.Identity.Application.Commands.ProvisionPortalAccount;

public sealed class ProvisionPortalAccountCommandHandler(
    IRepository<PortalAccount, PortalAccountId> accounts,
    ICustomersModuleApi customers,
    IPasswordHasher hasher,
    IDateTime dateTime,
    IIdentityUnitOfWork unitOfWork)
    : IRequestHandler<ProvisionPortalAccountCommand, Result<PortalAccountDto>>
{
    public async Task<Result<PortalAccountDto>> Handle(ProvisionPortalAccountCommand request, CancellationToken cancellationToken)
    {
        // Scoped and customers.read: an out-of-scope customer is a 403, an unknown one a 404, both from Customers.
        Result<CustomerSummary> customer = await customers.GetCustomerSummaryAsync(request.CustomerId, cancellationToken);

        if (customer.IsFailure)
        {
            return Result.Failure<PortalAccountDto>(customer.Error);
        }

        CustomerSummary summary = customer.Value;
        CustomerContactSummary? contact = summary.Contacts.FirstOrDefault(c => c.Id == request.ContactId);

        if (contact is null)
        {
            return Result.Failure<PortalAccountDto>(IdentityErrors.PortalContactNotFound(request.ContactId));
        }

        if (!summary.IsActive)
        {
            return Result.Failure<PortalAccountDto>(IdentityErrors.PortalCustomerInactive());
        }

        if (!contact.HasPortalAccess || contact.Email is null)
        {
            return Result.Failure<PortalAccountDto>(IdentityErrors.PortalAccessNotEnabled());
        }

        string hash = hasher.Hash(request.Password);
        DateTimeOffset now = dateTime.UtcNow;

        PortalAccount? account = await accounts.FirstOrDefaultAsync(
            new PortalAccountByContactSpecification(contact.Id), cancellationToken);

        if (account is not null)
        {
            if (account.TenantId.Value != summary.TenantId || account.CustomerId != summary.Id)
            {
                return Result.Failure<PortalAccountDto>(IdentityErrors.PortalContactNotFound(request.ContactId));
            }

            account.SetPasswordByAgent(hash, now);
        }
        else
        {
            if (!EmailAddress.TryCreate(contact.Email, out EmailAddress? email))
            {
                return Result.Failure<PortalAccountDto>(IdentityErrors.PortalAccessNotEnabled());
            }

            IReadOnlyList<PortalAccount> sameEmail = await accounts.ListAsync(
                new PortalAccountByEmailSpecification(email), cancellationToken);

            if (sameEmail.Count > 0)
            {
                return Result.Failure<PortalAccountDto>(IdentityErrors.PortalEmailInUse());
            }

            account = PortalAccount.Create(
                new TenantId(summary.TenantId),
                summary.Id,
                contact.Id,
                email,
                DisplayName.Create(contact.Name),
                PreferredLocale.Create(summary.PreferredLanguage),
                hash);
            accounts.Add(account);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new PortalAccountDto(
            account.Id.Value, account.CustomerId, account.ContactId, account.Email.Value, account.LastSignInAtUtc));
    }
}
