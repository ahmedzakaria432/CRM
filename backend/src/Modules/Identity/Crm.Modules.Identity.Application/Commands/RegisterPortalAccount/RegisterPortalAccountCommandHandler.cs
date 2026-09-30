using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Customers.Contracts;
using Crm.Modules.Identity.Application.Abstractions;
using Crm.Modules.Identity.Application.Specifications;
using Crm.Modules.Identity.Domain;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Modules.Identity.Application.Commands.RegisterPortalAccount;

/// <summary>D5: every outcome but malformed input is the same success. The password is hashed first on every path,
/// so the cost does not tell an unknown email from a known one.</summary>
public sealed partial class RegisterPortalAccountCommandHandler(
    IRepository<PortalAccount, PortalAccountId> accounts,
    ICustomersModuleApi customers,
    IPasswordHasher hasher,
    IIdentityUnitOfWork unitOfWork,
    ILogger<RegisterPortalAccountCommandHandler> logger)
    : IRequestHandler<RegisterPortalAccountCommand, Result>
{
    public async Task<Result> Handle(RegisterPortalAccountCommand request, CancellationToken cancellationToken)
    {
        string hash = hasher.Hash(request.Password);

        if (!EmailAddress.TryCreate(request.Email, out EmailAddress? email))
        {
            LogIgnored("malformed");
            return Result.Success();
        }

        IReadOnlyList<PortalAccount> sameEmail = await accounts.ListAsync(new PortalAccountByEmailSpecification(email), cancellationToken);

        if (sameEmail.Count > 0)
        {
            LogIgnored("taken");
            return Result.Success();
        }

        Result<IReadOnlyList<PortalContactMatch>> found = await customers.FindPortalContactsByEmailAsync(email.Value, cancellationToken);

        if (found.IsFailure)
        {
            return Result.Failure(found.Error);
        }

        if (found.Value.Count != 1)
        {
            LogIgnored(found.Value.Count == 0 ? "unknown" : "ambiguous");
            return Result.Success();
        }

        PortalContactMatch match = found.Value[0];
        IReadOnlyList<PortalAccount> sameContact = await accounts.ListAsync(
            new PortalAccountByContactSpecification(match.ContactId), cancellationToken);

        if (sameContact.Count > 0)
        {
            LogIgnored("contact-taken");
            return Result.Success();
        }

        var account = PortalAccount.Create(
            new TenantId(match.TenantId),
            match.CustomerId,
            match.ContactId,
            email,
            DisplayName.Create(match.ContactName),
            PreferredLocale.Create(request.PreferredLocale ?? match.PreferredLanguage),
            hash);

        accounts.Add(account);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        LogRegistered(account.Id.Value, match.CustomerId);
        return Result.Success();
    }

    // Never the email or the password: only the reason, and ids once an account exists.
    [LoggerMessage(Level = LogLevel.Information, Message = "Portal registration ignored ({Reason})")]
    private partial void LogIgnored(string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Portal account {AccountId} registered for customer {CustomerId}")]
    private partial void LogRegistered(Guid accountId, Guid customerId);
}
