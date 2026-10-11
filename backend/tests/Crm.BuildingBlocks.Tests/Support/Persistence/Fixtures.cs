using Crm.SharedKernel;
using Crm.SharedKernel.Scoping;

namespace Crm.BuildingBlocks.Tests.Support.Persistence;

// Entities for TestDbContext. Private parameterless constructors are for EF only; the public factories keep each row
// always valid. No IEntityTypeConfiguration anywhere in the test assembly: ModuleDbContext applies every
// configuration of its own assembly, so the model here is built from conventions (and TestDbContext.OnModelCreating).

public sealed class TenantRow : AggregateRoot<Guid>, ITenantScoped
{
    private TenantRow()
    {
    }

    public TenantId TenantId { get; private set; } = TenantId.None;

    public string Name { get; private set; } = string.Empty;

    public static TenantRow Create(Guid id, TenantId tenantId, string name) =>
        new() { Id = id, TenantId = tenantId, Name = name };
}

/// <summary>Scoped to one branch and department, audited, soft-deletable, with a row version.</summary>
public sealed class DepartmentRow : AuditableEntity<Guid>, IOrganisationScoped, ISoftDeletable, IHasRowVersion
{
    private DepartmentRow()
    {
    }

    public TenantId TenantId { get; private set; } = TenantId.None;

    public BranchId BranchId { get; private set; } = BranchId.New();

    public DepartmentId DepartmentId { get; private set; } = DepartmentId.New();

    public string Name { get; private set; } = string.Empty;

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public string? DeletedBy { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public static DepartmentRow Create(Guid id, TenantId tenantId, BranchId branchId, DepartmentId departmentId, string name) =>
        new() { Id = id, TenantId = tenantId, BranchId = branchId, DepartmentId = departmentId, Name = name };

    public void Rename(string name) => Name = name;

    public void MoveTo(BranchId branchId, DepartmentId departmentId)
    {
        BranchId = branchId;
        DepartmentId = departmentId;
    }
}

/// <summary>An aggregate one intention-revealing method may hand to another department.</summary>
public sealed class TransferableRow : AggregateRoot<Guid>, ITransferableOrganisationScoped
{
    private TransferableRow()
    {
    }

    public TenantId TenantId { get; private set; } = TenantId.None;

    public BranchId BranchId { get; private set; } = BranchId.New();

    public DepartmentId DepartmentId { get; private set; } = DepartmentId.New();

    public static TransferableRow Create(Guid id, TenantId tenantId, BranchId branchId, DepartmentId departmentId) =>
        new() { Id = id, TenantId = tenantId, BranchId = branchId, DepartmentId = departmentId };

    public void TransferTo(BranchId branchId, DepartmentId departmentId)
    {
        BranchId = branchId;
        DepartmentId = departmentId;
    }

    /// <summary>Not something a real aggregate offers - it lets a test attempt the forbidden tenant change.</summary>
    public void MoveToTenant(TenantId tenantId) => TenantId = tenantId;
}

public sealed class OwnedRow : AggregateRoot<Guid>, IPortalCustomerOwned
{
    private OwnedRow()
    {
    }

    public TenantId TenantId { get; private set; } = TenantId.None;

    public BranchId BranchId { get; private set; } = BranchId.New();

    public DepartmentId DepartmentId { get; private set; } = DepartmentId.New();

    public Guid CustomerId { get; private set; }

    public static OwnedRow Create(Guid id, TenantId tenantId, BranchId branchId, DepartmentId departmentId, Guid customerId) =>
        new() { Id = id, TenantId = tenantId, BranchId = branchId, DepartmentId = departmentId, CustomerId = customerId };
}

/// <summary>No scope marker at all: visible and writable to everyone. Physically deleted, never soft-deleted.</summary>
public sealed class UnscopedRow : AggregateRoot<Guid>, IHasRowVersion
{
    private UnscopedRow()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset? SeenAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public static UnscopedRow Create(Guid id, string name, DateTimeOffset? seenAtUtc = null) =>
        new() { Id = id, Name = name, SeenAtUtc = seenAtUtc };
}

public sealed class EventfulRow : AggregateRoot<Guid>
{
    private EventfulRow()
    {
    }

    public static EventfulRow Create(Guid id) => new() { Id = id };

    public void Touch(DateTimeOffset at) => Raise(new SampleDomainEvent(at));
}

/// <summary>A tenant-scoped, soft-deletable row with an owned value object, for the model-level assertions.</summary>
public sealed class ContactRow : AggregateRoot<Guid>, ITenantScoped, ISoftDeletable
{
    private ContactRow()
    {
    }

    public TenantId TenantId { get; private set; } = TenantId.None;

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public string? DeletedBy { get; private set; }

    public AddressValue Address { get; private set; } = new();

    public static ContactRow Create(Guid id, TenantId tenantId, string city) =>
        new() { Id = id, TenantId = tenantId, Address = new AddressValue { City = city } };
}

public sealed class AddressValue
{
    public string City { get; init; } = string.Empty;

    public DateTimeOffset? VerifiedAtUtc { get; init; }
}
