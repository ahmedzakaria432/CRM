using System.Linq.Expressions;
using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Customers.Domain;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Customers.Application.Specifications;

/// <summary>The paged search. No scope criteria on purpose - Story 06's filter adds tenant/branch/department.
/// Paging applies only when both page arguments are given (the count passes neither).</summary>
public sealed class CustomerSearchSpecification : Specification<Customer>
{
    public CustomerSearchSpecification(
        string? search, CustomerStatus? status, CustomerType? type, Guid? departmentId, Guid? branchId,
        CustomerSort sort, bool descending, int? page, int? pageSize)
    {
        string? term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        string? phoneTerm = term is null ? null : PhoneNumber.StripSeparators(term);
        if (phoneTerm is { Length: 0 })
        {
            phoneTerm = null;
        }

        bool anyStatus = status is null;
        CustomerStatus statusValue = status ?? CustomerStatus.Active;
        bool anyType = type is null;
        CustomerType typeValue = type ?? CustomerType.Individual;
        DepartmentId? department = departmentId is { } d ? new DepartmentId(d) : null;
        BranchId? branch = branchId is { } b ? new BranchId(b) : null;

        // One combined Where - Specification.Where replaces, it does not AND.
        Where(c =>
            (term == null
                || c.DisplayName.Arabic.Contains(term)
                || c.DisplayName.English.Contains(term)
                || c.Number.Value.Contains(term)
                || (c.LegalName != null && c.LegalName.Contains(term))
                || c.Contacts.Any(ct =>
                    (ct.Email != null && ct.Email.Value.Contains(term))
                    || (phoneTerm != null && ct.Phone != null && ct.Phone.Value.Contains(phoneTerm))))
            && (anyStatus || c.Status == statusValue)
            && (anyType || c.Type == typeValue)
            && (department == null || c.DepartmentId.Equals(department))
            && (branch == null || c.BranchId.Equals(branch)));

        Expression<Func<Customer, object>> key = sort switch
        {
            CustomerSort.Number => c => c.Number.Value,
            CustomerSort.NameEn => c => c.DisplayName.English,
            CustomerSort.NameAr => c => c.DisplayName.Arabic,
            _ => c => c.CreatedAtUtc ?? DateTimeOffset.MinValue,
        };

        if (descending)
        {
            ApplyOrderByDescending(key);
        }
        else
        {
            ApplyOrderBy(key);
        }

        ApplyThenBy(c => c.Number.Value);

        if (page is { } p && pageSize is { } size)
        {
            ApplyPaging((p - 1) * size, size);
        }

        AsReadOnly();
    }
}
