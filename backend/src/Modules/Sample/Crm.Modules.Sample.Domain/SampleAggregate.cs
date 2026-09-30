// CANONICAL SLICE - copy this shape for every new use case (CLAUDE.md, "Backend", CQRS).
// DELETE this whole Sample module in the story that lands the first real use case (Story 11).

using Crm.SharedKernel;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Sample.Domain;

/// <summary>Story 06: the first organisation-scoped aggregate - proves the scope filter, the write guard, and
/// OrganisationScope.ResolveWriteTarget end to end. Story 11 copies this shape onto Customer.</summary>
public sealed class SampleAggregate : AuditableEntity<SampleId>, ISoftDeletable, IHasRowVersion, IOrganisationScoped
{
    private readonly List<SampleLine> _lines = [];

    public SampleName Name { get; private set; }

    public TenantId TenantId { get; private set; } = TenantId.None;

    public BranchId BranchId { get; private set; } = new(Guid.Empty);

    public DepartmentId DepartmentId { get; private set; } = new(Guid.Empty);

    public byte[] RowVersion { get; private set; } = [];

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public string? DeletedBy { get; private set; }

    public IReadOnlyCollection<SampleLine> Lines => _lines.AsReadOnly();

    private SampleAggregate()
    {
        Name = null!;
    }

    private SampleAggregate(SampleId id, SampleName name, TenantId tenantId, BranchId branchId, DepartmentId departmentId) : base(id)
    {
        Name = name;
        TenantId = tenantId;
        BranchId = branchId;
        DepartmentId = departmentId;
    }

    /// <summary>
    /// <paramref name="id"/> defaults to a fresh id; the seeder passes a well-known one so its
    /// idempotency check has a stable value to look up instead of a freshly generated Guid that can
    /// never match.
    /// </summary>
    public static SampleAggregate Create(
        SampleName name, IEnumerable<string> lines, TenantId tenantId, BranchId branchId, DepartmentId departmentId, SampleId? id = null)
    {
        ArgumentNullException.ThrowIfNull(lines);

        if (tenantId == TenantId.None)
        {
            throw new DomainException(new DomainError("Sample.ScopeRequired", "A sample must belong to a tenant, branch, and department."));
        }

        // The first invariant only the aggregate can enforce - CreateSampleCommandValidator checks each line is
        // not empty, not that lines are distinct. Surfaces as 422 urn:crm:problem:domain-rule (Story 03).
        List<string> lineList = lines.ToList();
        if (lineList.Count != lineList.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            throw new DomainException(new DomainError("Sample.DuplicateLine", "A sample cannot contain the same line twice."));
        }

        var aggregate = new SampleAggregate(id ?? SampleId.New(), name, tenantId, branchId, departmentId);

        foreach (string line in lineList)
        {
            aggregate._lines.Add(new SampleLine(line));
        }

        return aggregate;
    }

    /// <summary>
    /// Takes the event timestamp as a parameter rather than reading <c>DateTimeOffset.UtcNow</c>
    /// directly - BannedSymbols.txt bans that read solution-wide (Story 02 Task 11) so the clock is
    /// only ever reachable through the injected IDateTime, resolved by the caller.
    /// </summary>
    public void Rename(SampleName name, DateTimeOffset occurredOnUtc)
    {
        Name = name;
        Raise(new SampleRenamed(Id, occurredOnUtc));
    }
}
