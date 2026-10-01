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
        var riyadhBilling = new DepartmentId(OrganisationDevSeed.RiyadhBillingDepartmentId);
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

        if (!await ExistsAsync(CustomersDevSeed.AlNoorClinicsCustomerId, cancellationToken))
        {
            Customer alNoor = Customer.Create(
                await numbers.NextAsync(cancellationToken),
                LocalizedText.Create("مجمع النور الطبي", "Al-Noor Clinics"),
                CustomerType.Organisation, "Al-Noor Medical Clinics Co.", PreferredLanguage.Arabic, tenant, riyadhBranch, riyadhCare,
                new CustomerId(CustomersDevSeed.AlNoorClinicsCustomerId));

            alNoor.AddContact(
                "Mona Al-Saleh", "Operations Manager", EmailAddress.Create(CustomersDevSeed.AlNoorMonaEmail), null,
                isPrimary: true, hasPortalAccess: true, CustomersDevSeed.AlNoorMonaContactId);
            alNoor.AddContact(
                "Yousef Al-Amri", "Front desk", EmailAddress.Create(CustomersDevSeed.AlNoorYousefEmail), PhoneNumber.Create("+966503334455"),
                isPrimary: false, hasPortalAccess: true, CustomersDevSeed.AlNoorYousefContactId);
            alNoor.AddContact(
                "Huda Al-Shehri", "Accounts", EmailAddress.Create(CustomersDevSeed.AlNoorHudaEmail), null,
                isPrimary: false, hasPortalAccess: false, CustomersDevSeed.AlNoorHudaContactId);

            await SaveAsync(alNoor, cancellationToken);
        }

        if (!await ExistsAsync(CustomersDevSeed.FaisalCustomerId, cancellationToken))
        {
            Customer faisal = Customer.Create(
                await numbers.NextAsync(cancellationToken),
                LocalizedText.Create("فيصل الدوسري", "Faisal Al-Dosari"),
                CustomerType.Individual, null, PreferredLanguage.English, tenant, riyadhBranch, riyadhCare,
                new CustomerId(CustomersDevSeed.FaisalCustomerId));

            faisal.AddContact(
                "Faisal Al-Dosari", null, EmailAddress.Create(CustomersDevSeed.FaisalEmail), PhoneNumber.Create("+966505556677"),
                isPrimary: true, hasPortalAccess: true, CustomersDevSeed.FaisalContactId);

            await SaveAsync(faisal, cancellationToken);
        }

        if (!await ExistsAsync(CustomersDevSeed.GulfRetailCustomerId, cancellationToken))
        {
            Customer gulfRetail = Customer.Create(
                await numbers.NextAsync(cancellationToken),
                LocalizedText.Create("الخليج للتجزئة", "Gulf Retail"),
                CustomerType.Organisation, "Gulf Retail Trading Co.", PreferredLanguage.English, tenant, riyadhBranch, riyadhBilling,
                new CustomerId(CustomersDevSeed.GulfRetailCustomerId));

            gulfRetail.AddContact(
                "Nasser Al-Qahtani", "Finance", EmailAddress.Create("nasser@gulfretail.example.com"), null,
                isPrimary: true, hasPortalAccess: false, CustomersDevSeed.GulfRetailNasserContactId);

            await SaveAsync(gulfRetail, cancellationToken);
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
