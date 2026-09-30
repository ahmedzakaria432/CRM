using Crm.SharedKernel;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Organisation.Domain;

/// <summary>One user's role in one department. A user can hold several memberships (several departments, a
/// different role in each). UserId is a plain Guid - users (and their aggregate) arrive in Story 07.</summary>
public sealed class Membership : AuditableEntity<MembershipId>, ITenantScoped, ISoftDeletable, IHasRowVersion
{
    public TenantId TenantId { get; private set; } = TenantId.None;

    public Guid UserId { get; private set; }

    public DepartmentId DepartmentId { get; private set; } = new(Guid.Empty);

    public BranchId BranchId { get; private set; } = new(Guid.Empty);

    public SystemRole Role { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public string? DeletedBy { get; private set; }

    private Membership()
    {
    }

    private Membership(MembershipId id, TenantId tenantId, Guid userId, DepartmentId departmentId, BranchId branchId, SystemRole role)
        : base(id)
    {
        TenantId = tenantId;
        UserId = userId;
        DepartmentId = departmentId;
        BranchId = branchId;
        Role = role;
    }

    /// <summary>BranchId is copied from the department at creation - departments never move between branches
    /// (reparenting is out of scope), so the copy never goes stale.</summary>
    public static Membership Create(Department department, Guid userId, SystemRole role, MembershipId? id = null)
    {
        ArgumentNullException.ThrowIfNull(department);

        if (userId == Guid.Empty)
        {
            throw new DomainException(new DomainError("Organisation.UserRequired", "A membership must name a user."));
        }

        if (!department.IsActive)
        {
            throw new DomainException(new DomainError("Organisation.DepartmentInactive", "The department is inactive."));
        }

        return new Membership(id ?? MembershipId.New(), department.TenantId, userId, department.Id, department.BranchId, role);
    }

    public void ChangeRole(SystemRole role) => Role = role;
}
