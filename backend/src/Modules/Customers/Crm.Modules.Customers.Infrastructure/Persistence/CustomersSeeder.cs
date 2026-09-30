using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Infrastructure.Modules;
using Crm.BuildingBlocks.Infrastructure.Persistence;
using Crm.Modules.Customers.Application.Abstractions;
using Crm.Modules.Customers.Contracts.DevSeed;
using Crm.Modules.Customers.Domain;
using Crm.Modules.Organisation.Contracts.DevSeed;
using Crm.SharedKernel.Scoping;
using Microsoft.EntityFrameworkCore;

namespace Crm.Modules.Customers.Infrastructure.Persistence;

public sealed class CustomersSeeder(CustomersDbContext context, ICustomerNumberGenerator numbers, IDateTime dateTime) : IModuleSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var tenant = new TenantId(OrganisationDevSeed.TenantId);
        var riyadhBranch = new BranchId(OrganisationDevSeed.RiyadhBranchId);
        var riyadhCare = new DepartmentId(OrganisationDevSeed.RiyadhCareDepartmentId);
        var jeddahBranch = new BranchId(OrganisationDevSeed.JeddahBranchId);
        var jeddahCare = new DepartmentId(OrganisationDevSeed.JeddahCareDepartmentId);

        if (!await ExistsAsync(CustomersDevSeed.AhmedCustomerId, cancellationToken))
        {
            Customer ahmed = Customer.Create(
                await numbers.NextAsync(cancellationToken),
                LocalizedText.Create("أحمد القحطاني", "Ahmed Al-Qahtani"),
                CustomerType.Individual, null, PreferredLanguage.Arabic, tenant, riyadhBranch, riyadhCare,
                new CustomerId(CustomersDevSeed.AhmedCustomerId));

            ahmed.AddContact(
                "Ahmed Al-Qahtani", null, EmailAddress.Create(CustomersDevSeed.AhmedEmail), PhoneNumber.Create(CustomersDevSeed.AhmedPhone),
                isPrimary: true, hasPortalAccess: true, CustomersDevSeed.AhmedContactId);

            await SaveAsync(ahmed, cancellationToken);
        }

        if (!await ExistsAsync(CustomersDevSeed.NajdLogisticsCustomerId, cancellationToken))
        {
            Customer najd = Customer.Create(
                await numbers.NextAsync(cancellationToken),
                LocalizedText.Create("نجد للخدمات اللوجستية", "Najd Logistics"),
                CustomerType.Organisation, "Najd Logistics Company Ltd.", PreferredLanguage.English, tenant, riyadhBranch, riyadhCare,
                new CustomerId(CustomersDevSeed.NajdLogisticsCustomerId));

            najd.AddContact(
                "Sara Al-Otaibi", "Procurement Manager", EmailAddress.Create("sara@najd.example.com"), null,
                isPrimary: true, hasPortalAccess: false, CustomersDevSeed.NajdSaraContactId);
            najd.AddContact(
                "Omar Haddad", "Accounts", null, PhoneNumber.Create("+966551112233"),
                isPrimary: false, hasPortalAccess: false, CustomersDevSeed.NajdOmarContactId);

            najd.AddNote(
                "Key account - escalate billing questions to the supervisor.", OrganisationDevSeed.AdminUserId, dateTime.UtcNow,
                CustomersDevSeed.NajdPinnedNoteId);
            najd.PinNote(CustomersDevSeed.NajdPinnedNoteId, dateTime.UtcNow);

            await SaveAsync(najd, cancellationToken);
        }

        if (!await ExistsAsync(CustomersDevSeed.RedSeaTradingCustomerId, cancellationToken))
        {
            Customer redSea = Customer.Create(
                await numbers.NextAsync(cancellationToken),
                LocalizedText.Create("تجارة البحر الأحمر", "Red Sea Trading"),
                CustomerType.Organisation, "Red Sea Trading Est.", PreferredLanguage.Arabic, tenant, jeddahBranch, jeddahCare,
                new CustomerId(CustomersDevSeed.RedSeaTradingCustomerId));

            redSea.AddContact(
                "Fahad Al-Ghamdi", null, EmailAddress.Create("fahad@redsea.example.com"), PhoneNumber.Create("+966122223344"),
                isPrimary: true, hasPortalAccess: false, CustomersDevSeed.RedSeaFahadContactId);

            await SaveAsync(redSea, cancellationToken);
        }
    }

    // Keyed on a stable well-known id, never a freshly generated Guid. Both named filters are lifted: the seeder
    // must see soft-deleted rows too, and runs with no HttpContext (OrganisationScope.Unrestricted).
    private Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken)
    {
        var customerId = new CustomerId(id);
        return context.Customers
            .IgnoreQueryFilters([ModuleDbContext.SoftDeleteFilterName, ModuleDbContext.ScopeFilterName])
            .AnyAsync(c => c.Id == customerId, cancellationToken);
    }

    private async Task SaveAsync(Customer customer, CancellationToken cancellationToken)
    {
        context.Customers.Add(customer);
        await context.SaveChangesAsync(cancellationToken);
    }
}
