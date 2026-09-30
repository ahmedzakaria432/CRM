using Crm.Modules.Customers.Application.Abstractions;
using Crm.Modules.Customers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Crm.Modules.Customers.Infrastructure.Persistence;

/// <summary>Un-composed on purpose: SQL Server rejects NEXT VALUE FOR inside a subquery, and First()/Single()
/// after SqlQuery wraps the SQL in one. The literal SQL must stay a literal - an interpolation hole here becomes a
/// SQL parameter, not a schema name. Runs on the command's open transaction (same DbContext).</summary>
public sealed class SqlCustomerNumberGenerator(CustomersDbContext context) : ICustomerNumberGenerator
{
    public async Task<CustomerNumber> NextAsync(CancellationToken cancellationToken = default)
    {
        List<long> values = await context.Database
            .SqlQuery<long>($"SELECT NEXT VALUE FOR [customers].[CustomerNumbers] AS [Value]")
            .ToListAsync(cancellationToken);

        return CustomerNumber.FromSequence(values[0]);
    }
}
