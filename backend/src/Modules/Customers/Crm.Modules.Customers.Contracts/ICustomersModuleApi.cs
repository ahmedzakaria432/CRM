using Crm.SharedKernel.Results;

namespace Crm.Modules.Customers.Contracts;

/// <summary>What another module may ask the Customers module, synchronously. Implemented by sending the module's
/// own queries, so validation, authorisation (customers.read) and organisation scoping apply: an existing customer
/// outside the caller's scope is a failed Result (403 Organisation.OutOfScope), an unknown id a failed Result
/// (404 Customers.CustomerNotFound). Primitives and contract DTOs only.</summary>
public interface ICustomersModuleApi
{
    /// <summary>Story 15: does this customer exist in my scope, which department owns it, which contacts does it have.</summary>
    Task<Result<CustomerSummary>> GetCustomerSummaryAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>Story 20: number and names of each customer the caller may see (customers.read + scope). Unknown and
    /// out-of-scope ids are simply absent - success, never 403. At most 100 ids.</summary>
    Task<Result<IReadOnlyList<CustomerNameSummary>>> GetCustomerNamesAsync(
        IReadOnlyCollection<Guid> customerIds, CancellationToken cancellationToken = default);

    /// <summary>Staff only (customers.read, caller's scope); the portal uses FindPortalContactsByEmailAsync. Every
    /// in-scope contact whose email equals <paramref name="email"/> (normalised). A
    /// malformed email is an empty list, not a failure.</summary>
    Task<Result<IReadOnlyList<CustomerContactMatch>>> FindContactByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Story 47, in-process sign-in lookup (no HTTP endpoint, anonymous, scope lifted - reviewed): every contact
    /// WITH PORTAL ACCESS of an ACTIVE customer, in any tenant, whose normalised email equals <paramref name="email"/>. A
    /// malformed email is an empty list. At most 20.</summary>
    Task<Result<IReadOnlyList<PortalContactMatch>>> FindPortalContactsByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Story 47, in-process sign-in/refresh lookup: the contact if it still has portal access and its customer is
    /// active; otherwise a failed Result (404 Customers.PortalContactNotFound).</summary>
    Task<Result<PortalContactMatch>> GetPortalContactAsync(Guid contactId, CancellationToken cancellationToken = default);
}

public sealed record PortalContactMatch(
    Guid CustomerId,
    Guid ContactId,
    string ContactName,
    string Email,
    bool IsPrimary,
    string PreferredLanguage,
    Guid TenantId,
    Guid BranchId,
    Guid DepartmentId);

public sealed record CustomerSummary(
    Guid Id,
    string Number,
    string DisplayNameAr,
    string DisplayNameEn,
    string Type,
    bool IsActive,
    string PreferredLanguage,
    Guid TenantId,
    Guid BranchId,
    Guid DepartmentId,
    IReadOnlyList<CustomerContactSummary> Contacts);

public sealed record CustomerContactSummary(Guid Id, string Name, string? Email, string? Phone, bool IsPrimary, bool HasPortalAccess);

public sealed record CustomerContactMatch(
    Guid CustomerId,
    string CustomerNumber,
    bool IsCustomerActive,
    Guid ContactId,
    string ContactName,
    string Email,
    bool HasPortalAccess,
    Guid TenantId,
    Guid BranchId,
    Guid DepartmentId);

public sealed record CustomerNameSummary(Guid Id, string Number, string DisplayNameAr, string DisplayNameEn);
