// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample folder in the story that lands the first real use case (Story 11).

using Crm.Domain.Common;

namespace Crm.Domain.Sample;

/// <summary>Child entity - no DbSet, no repository. Reachable only through SampleAggregate.</summary>
public sealed class SampleLine : Entity<Guid>
{
    public string Text { get; private set; }

    private SampleLine()
    {
        Text = string.Empty;
    }

    public SampleLine(string text) : base(Guid.NewGuid())
    {
        Text = text;
    }
}
