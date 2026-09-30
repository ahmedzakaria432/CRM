using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Messaging;
using Crm.Modules.Customers.Application.Abstractions;
using Crm.Modules.Customers.Application.Specifications;
using Crm.Modules.Customers.Contracts.IntegrationEvents;
using Crm.Modules.Customers.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Customers.Application.Commands.CreateCustomer;

public sealed class CreateCustomerCommandHandler(
    IRepository<Customer, CustomerId> repository,
    ICustomersUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    ICustomerNumberGenerator numbers,
    IDateTime dateTime,
    IEventBus eventBus)
    : IRequestHandler<CreateCustomerCommand, Result<CreateCustomerOutcome>>
{
    public async Task<Result<CreateCustomerOutcome>> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        ScopeWriteTarget? target = currentUser.Scope.ResolveWriteTarget(request.DepartmentId);
        if (target is null)
        {
            return Result.Failure<CreateCustomerOutcome>(request.DepartmentId is not null
                ? CustomersErrors.DepartmentOutOfScope()
                : CustomersErrors.DepartmentRequired());
        }

        // Every value object first: a malformed email or phone is 422 before any duplicate lookup.
        LocalizedText displayName = LocalizedText.Create(request.DisplayNameAr, request.DisplayNameEn);
        CustomerType type = Enum.Parse<CustomerType>(request.Type, ignoreCase: true);
        PreferredLanguage language = PreferredLanguage.Create(request.PreferredLanguage);
        List<(NewContact Input, EmailAddress? Email, PhoneNumber? Phone)> contacts =
            [.. request.Contacts.Select(c =>
            {
                (EmailAddress? email, PhoneNumber? phone) = ContactChannels.Parse(c.Email, c.Phone);
                return (c, email, phone);
            })];

        if (!request.ConfirmDuplicate)
        {
            List<string> emails = [.. contacts.Select(c => c.Email).OfType<EmailAddress>().Select(e => e.Value).Distinct(StringComparer.Ordinal)];
            List<string> phones = [.. contacts.Select(c => c.Phone).OfType<PhoneNumber>().Select(p => p.Value).Distinct(StringComparer.Ordinal)];

            if (emails.Count > 0 || phones.Count > 0)
            {
                IReadOnlyList<Customer> matches = await repository.ListAsync(
                    new CustomersWithContactChannelsSpecification(emails, phones), cancellationToken);

                if (matches.Count > 0)
                {
                    return Result.Success(CreateCustomerOutcome.Duplicates(
                        [.. matches.Select(m => DuplicateCandidates.From(m, emails, phones))]));
                }
            }
        }

        CustomerNumber number = await numbers.NextAsync(cancellationToken);
        Customer customer = Customer.Create(
            number, displayName, type, request.LegalName, language, target.TenantId, target.BranchId, target.DepartmentId);

        foreach ((NewContact input, EmailAddress? email, PhoneNumber? phone) in contacts)
        {
            customer.AddContact(input.Name, input.JobTitle, email, phone, input.IsPrimary, input.HasPortalAccess);
        }

        repository.Add(customer);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        eventBus.Publish(new CustomerCreatedIntegrationEvent(
            Guid.NewGuid(), dateTime.UtcNow, customer.Id.Value, customer.Number.Value, customer.DisplayName.Arabic,
            customer.DisplayName.English, customer.Type.ToString(), customer.TenantId.Value, customer.BranchId.Value,
            customer.DepartmentId.Value));

        return Result.Success(CreateCustomerOutcome.Success(CustomerDto.FromDomain(customer)));
    }
}
