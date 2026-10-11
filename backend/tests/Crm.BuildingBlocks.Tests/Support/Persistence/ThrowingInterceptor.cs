using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Crm.BuildingBlocks.Tests.Support.Persistence;

/// <summary>InMemory never raises a concurrency conflict on its own, so this raises one from SaveChanges.</summary>
public sealed class ThrowingInterceptor : SaveChangesInterceptor
{
    public DbUpdateConcurrencyException Thrown { get; } = new("simulated conflict");

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
        throw Thrown;
}
