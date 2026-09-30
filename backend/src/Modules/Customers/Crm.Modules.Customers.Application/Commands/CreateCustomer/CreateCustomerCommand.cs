using Crm.BuildingBlocks.Application.Authorization;
using Crm.BuildingBlocks.Application.Messaging;

namespace Crm.Modules.Customers.Application.Commands.CreateCustomer;

/// <summary>DepartmentId is optional: null resolves through OrganisationScope.ResolveWriteTarget. With
/// ConfirmDuplicate false, a contact email/phone that matches an in-scope customer creates nothing and returns the
/// candidates instead.</summary>
[RequirePermission(CrmPermissions.CustomersWrite)]
public sealed record CreateCustomerCommand(
    string? DisplayNameAr, string? DisplayNameEn, string Type, string? LegalName, string PreferredLanguage,
    IReadOnlyList<NewContact> Contacts, bool ConfirmDuplicate, Guid? DepartmentId) : ICommand<CreateCustomerOutcome>;

public sealed record NewContact(string Name, string? JobTitle, string? Email, string? Phone, bool IsPrimary, bool HasPortalAccess);
