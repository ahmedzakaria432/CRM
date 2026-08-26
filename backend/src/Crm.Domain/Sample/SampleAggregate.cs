// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample folder in the story that lands the first real use case (Story 11).

using Crm.Domain.Common;

namespace Crm.Domain.Sample;

public sealed class SampleAggregate : AuditableEntity<SampleId>, ISoftDeletable, IHasRowVersion
{
    private readonly List<SampleLine> _lines = [];

    public SampleName Name { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public string? DeletedBy { get; private set; }

    public IReadOnlyCollection<SampleLine> Lines => _lines.AsReadOnly();

    private SampleAggregate()
    {
        Name = null!;
    }

    private SampleAggregate(SampleId id, SampleName name) : base(id)
    {
        Name = name;
    }

    /// <summary>
    /// <paramref name="id"/> defaults to a fresh id; the seeder passes a well-known one so its
    /// idempotency check has a stable value to look up instead of a freshly generated Guid that can
    /// never match.
    /// </summary>
    public static SampleAggregate Create(SampleName name, IEnumerable<string> lines, SampleId? id = null)
    {
        var aggregate = new SampleAggregate(id ?? SampleId.New(), name);

        foreach (string line in lines)
        {
            aggregate._lines.Add(new SampleLine(line));
        }

        return aggregate;
    }

    /// <summary>
    /// Takes the event timestamp as a parameter rather than reading <c>DateTimeOffset.UtcNow</c>
    /// directly - BannedSymbols.txt bans that read solution-wide (Story 02 Task 11) so the clock is
    /// only ever reachable through the injected TimeProvider, resolved by the caller.
    /// </summary>
    public void Rename(SampleName name, DateTimeOffset occurredOnUtc)
    {
        Name = name;
        Raise(new SampleRenamed(Id, occurredOnUtc));
    }
}
