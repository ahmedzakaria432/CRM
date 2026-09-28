// CANONICAL SLICE - the shape of a module's synchronous public API.
// DELETE this whole Sample module in the story that lands the first real use case (Story 11).

using Crm.SharedKernel.Results;

namespace Crm.Modules.Sample.Contracts;

/// <summary>
/// What another module may ask the Sample module, synchronously. A module's Contracts project is
/// the ONLY thing other modules can reference (Directory.Build.targets, rule 1), so everything here
/// is primitives and contract DTOs - never a Sample domain type, never an entity.
/// </summary>
/// <remarks>
/// Use a Contracts API for a read another module needs right now (e.g. Tickets checking that a
/// customer exists). Use an integration event, also published from Contracts, for "this happened"
/// - never call another module to tell it to change its own state inside your transaction.
/// </remarks>
public interface ISampleModuleApi
{
    Task<Result<SampleSummary>> GetSampleAsync(Guid sampleId, CancellationToken cancellationToken = default);
}

public sealed record SampleSummary(Guid Id, string Name);
