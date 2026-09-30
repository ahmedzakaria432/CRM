using Crm.SharedKernel;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Tickets.Domain;

/// <summary>A flat, bilingual ticket category owned by one department (Story 15; hierarchy deferred). Tenant-scoped,
/// not department-scoped: its name stays readable on every ticket that carries it. The department never changes.</summary>
public sealed class Category : AuditableEntity<CategoryId>, ITenantScoped, IHasRowVersion
{
    public const int DisplayOrderMax = 9999;

    public TenantId TenantId { get; private set; } = TenantId.None;

    public DepartmentId DepartmentId { get; private set; } = new(Guid.Empty);

    public LocalizedText Name { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsCustomerVisible { get; private set; }

    public bool IsActive { get; private set; } = true;

    public byte[] RowVersion { get; private set; } = [];

    private Category()
    {
        Name = null!;
    }

    private Category(CategoryId id, TenantId tenantId, DepartmentId departmentId, LocalizedText name, int displayOrder, bool isCustomerVisible)
        : base(id)
    {
        TenantId = tenantId;
        DepartmentId = departmentId;
        Name = name;
        DisplayOrder = displayOrder;
        IsCustomerVisible = isCustomerVisible;
    }

    public static Category Create(
        TenantId tenantId, DepartmentId departmentId, LocalizedText name, int displayOrder, bool isCustomerVisible, CategoryId? id = null)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        ArgumentNullException.ThrowIfNull(departmentId);
        ArgumentNullException.ThrowIfNull(name);

        if (tenantId == TenantId.None || departmentId.Value == Guid.Empty)
        {
            throw new DomainException(TicketDomainErrors.CategoryScopeRequired);
        }

        return new Category(id ?? CategoryId.New(), tenantId, departmentId, name, EnsureOrder(displayOrder), isCustomerVisible);
    }

    public void Update(LocalizedText name, int displayOrder, bool isCustomerVisible)
    {
        ArgumentNullException.ThrowIfNull(name);

        Name = name;
        DisplayOrder = EnsureOrder(displayOrder);
        IsCustomerVisible = isCustomerVisible;
    }

    public void Deactivate()
    {
        if (!IsActive)
        {
            throw new DomainException(TicketDomainErrors.CategoryAlreadyInactive);
        }

        IsActive = false;
    }

    public void Reactivate()
    {
        if (IsActive)
        {
            throw new DomainException(TicketDomainErrors.CategoryAlreadyActive);
        }

        IsActive = true;
    }

    private static int EnsureOrder(int order) =>
        order is >= 0 and <= DisplayOrderMax ? order : throw new DomainException(TicketDomainErrors.CategoryDisplayOrderInvalid);
}
