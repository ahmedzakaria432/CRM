using Crm.Modules.Customers.Domain;

namespace Crm.Modules.Customers.Application.Abstractions;

/// <summary>Draws the next value of the customers.CustomerNumbers sequence. Not transactional: a rolled-back create
/// leaves a gap.</summary>
public interface ICustomerNumberGenerator
{
    Task<CustomerNumber> NextAsync(CancellationToken cancellationToken = default);
}
