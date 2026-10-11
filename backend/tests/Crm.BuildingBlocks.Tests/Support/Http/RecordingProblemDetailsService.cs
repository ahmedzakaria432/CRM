using Microsoft.AspNetCore.Http;

namespace Crm.BuildingBlocks.Tests.Support.Http;

/// <summary>Records every problem written. A hand-written double: <c>ValueTask</c> results cannot be configured on a
/// substitute without tripping CA2012.</summary>
public sealed class RecordingProblemDetailsService(bool writeSucceeds = true) : IProblemDetailsService
{
    public List<ProblemDetailsContext> Written { get; } = [];

    public ValueTask WriteAsync(ProblemDetailsContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Written.Add(context);
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Written.Add(context);
        return ValueTask.FromResult(writeSucceeds);
    }
}
