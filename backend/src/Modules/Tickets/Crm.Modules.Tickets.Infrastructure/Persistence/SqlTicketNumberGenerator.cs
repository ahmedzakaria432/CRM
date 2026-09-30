using Crm.Modules.Tickets.Application.Abstractions;
using Crm.Modules.Tickets.Domain;
using Microsoft.EntityFrameworkCore;

namespace Crm.Modules.Tickets.Infrastructure.Persistence;

/// <summary>Un-composed on purpose: SQL Server rejects NEXT VALUE FOR inside a subquery, and First()/Single()
/// after SqlQuery wraps the SQL in one. The literal SQL must stay a literal - an interpolation hole here becomes a
/// SQL parameter, not a schema name. Runs on the command open transaction (same DbContext).</summary>
public sealed class SqlTicketNumberGenerator(TicketsDbContext context) : ITicketNumberGenerator
{
    public async Task<TicketNumber> NextAsync(CancellationToken cancellationToken = default)
    {
        List<long> values = await context.Database
            .SqlQuery<long>($"SELECT NEXT VALUE FOR [tickets].[TicketNumbers] AS [Value]")
            .ToListAsync(cancellationToken);

        return TicketNumber.FromSequence(values[0]);
    }
}
