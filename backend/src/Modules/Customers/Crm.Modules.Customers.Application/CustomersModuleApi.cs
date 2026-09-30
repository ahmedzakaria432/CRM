using Crm.Modules.Customers.Application.Queries.FindContactsByEmail;
using Crm.Modules.Customers.Application.Queries.GetCustomerById;
using Crm.Modules.Customers.Application.Queries.FindPortalContactsByEmail;
using Crm.Modules.Customers.Application.Queries.GetCustomerNames;
using Crm.Modules.Customers.Application.Queries.GetPortalContact;
using Crm.Modules.Customers.Contracts;
using Crm.Modules.Customers.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Customers.Application;

/// <summary>Goes through the module's own queries, so a cross-module read passes the same validation,
/// authorisation, and scoping pipeline as an HTTP read - there is no back door into the module.</summary>
public sealed class CustomersModuleApi(ISender sender) : ICustomersModuleApi
{
    public async Task<Result<CustomerSummary>> GetCustomerSummaryAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        Result<CustomerDto> result = await sender.Send(new GetCustomerByIdQuery(customerId), cancellationToken);

        return result.IsSuccess
            ? Result.Success(ToSummary(result.Value))
            : Result.Failure<CustomerSummary>(result.Error);
    }

    public Task<Result<IReadOnlyList<CustomerNameSummary>>> GetCustomerNamesAsync(
        IReadOnlyCollection<Guid> customerIds, CancellationToken cancellationToken = default) =>
        sender.Send(new GetCustomerNamesQuery([.. customerIds]), cancellationToken);

    public Task<Result<IReadOnlyList<CustomerContactMatch>>> FindContactByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        sender.Send(new FindContactsByEmailQuery(email), cancellationToken);

    public Task<Result<IReadOnlyList<PortalContactMatch>>> FindPortalContactsByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        sender.Send(new FindPortalContactsByEmailQuery(email), cancellationToken);

    public Task<Result<PortalContactMatch>> GetPortalContactAsync(Guid contactId, CancellationToken cancellationToken = default) =>
        sender.Send(new GetPortalContactQuery(contactId), cancellationToken);

    private static CustomerSummary ToSummary(CustomerDto c) => new(
        c.Id, c.Number, c.DisplayName.Ar, c.DisplayName.En, c.Type, c.Status == nameof(CustomerStatus.Active),
        c.PreferredLanguage, c.TenantId, c.BranchId, c.DepartmentId,
        [.. c.Contacts.Select(x => new CustomerContactSummary(x.Id, x.Name, x.Email, x.Phone, x.IsPrimary, x.HasPortalAccess))]);
}
