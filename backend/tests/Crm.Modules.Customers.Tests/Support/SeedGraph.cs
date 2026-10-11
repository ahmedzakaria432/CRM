using Crm.Modules.Customers.Domain;
using Crm.Modules.Customers.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Modules.Customers.Tests.Support;

/// <summary>The customers the query tests read, written into one InMemory database through an unrestricted context.
/// Each is stamped one minute after the previous one, so the creation order is C-000001 ... C-000005.</summary>
public sealed class SeedGraph
{
    private SeedGraph(string database, Customer ahmed, Customer najd, Customer redSea, Customer faisal, Customer foreign, Customer deleted)
    {
        Database = database;
        Ahmed = ahmed;
        Najd = najd;
        RedSea = redSea;
        Faisal = faisal;
        Foreign = foreign;
        Deleted = deleted;
    }

    public string Database { get; }

    /// <summary>The customer number of one of the seeded customers.</summary>
    public string NumberOf(Guid customerId) =>
        new[] { Ahmed, Najd, RedSea, Faisal, Foreign, Deleted }.Single(c => c.Id.Value == customerId).Number.Value;

    /// <summary>Individual, active, department A1; one primary contact with email and phone and portal access.</summary>
    public Customer Ahmed { get; }

    /// <summary>Organisation, active, department A1; two contacts (Sara by email, Omar by phone) and a legal name.</summary>
    public Customer Najd { get; }

    /// <summary>Organisation, INACTIVE, department A2 of tenant A; one contact with email, phone and portal access.</summary>
    public Customer RedSea { get; }

    /// <summary>Individual, active, department A1, no contacts.</summary>
    public Customer Faisal { get; }

    /// <summary>Organisation, active, tenant B, department B1; one contact.</summary>
    public Customer Foreign { get; }

    /// <summary>Active, department A1, soft-deleted.</summary>
    public Customer Deleted { get; }

    public static async Task<SeedGraph> Create()
    {
        string database = CustomersTestContext.NewDatabaseName();

        Customer ahmed = CustomerFixtures.Individual(1, "أحمد القحطاني", "Ahmed Al-Qahtani", "ar", id: new CustomerId(Guid.Parse("c1000000-0000-0000-0000-000000000001")));
        ahmed.AddContact(
            "Ahmed", "Owner", CustomerFixtures.Email("ahmed@example.com"), CustomerFixtures.Phone("+966501234567"), true, true,
            Guid.Parse("c2000000-0000-0000-0000-000000000001"));

        Customer najd = CustomerFixtures.Organisation(
            2, "نجد", "Najd Logistics", "Najd Logistics Company Ltd.", id: new CustomerId(Guid.Parse("c1000000-0000-0000-0000-000000000002")));
        najd.AddContact("Sara", null, CustomerFixtures.Email("sara@najd.example.com"), null, true, false, Guid.Parse("c2000000-0000-0000-0000-000000000002"));
        najd.AddContact("Omar", null, null, CustomerFixtures.Phone("+966551112233"), false, false, Guid.Parse("c2000000-0000-0000-0000-000000000003"));

        Customer redSea = CustomerFixtures.Organisation(
            3, "البحر الأحمر", "Red Sea Trading", "Red Sea Trading Est.", "ar", Ids.TenantA, Ids.BranchA2, Ids.DepartmentA2,
            new CustomerId(Guid.Parse("c1000000-0000-0000-0000-000000000003")));
        redSea.AddContact(
            "Fahad", null, CustomerFixtures.Email("fahad@redsea.example.com"), CustomerFixtures.Phone("+966122223344"), true, true,
            Guid.Parse("c2000000-0000-0000-0000-000000000004"));
        redSea.Deactivate();

        Customer faisal = CustomerFixtures.Individual(4, "فيصل", "Faisal Al-Dosari", id: new CustomerId(Guid.Parse("c1000000-0000-0000-0000-000000000004")));

        Customer foreign = CustomerFixtures.Organisation(
            5, "أجنبي", "Foreign Co", "Foreign Company", "en", Ids.TenantB, Ids.BranchB1, Ids.DepartmentB1,
            new CustomerId(Guid.Parse("c1000000-0000-0000-0000-000000000005")));
        foreign.AddContact(
            "Frank", null, CustomerFixtures.Email("ahmed@example.com"), null, true, true, Guid.Parse("c2000000-0000-0000-0000-000000000005"));

        Customer deleted = CustomerFixtures.Individual(6, "محذوف", "Deleted One", id: new CustomerId(Guid.Parse("c1000000-0000-0000-0000-000000000006")));
        deleted.AddContact("Gone", null, CustomerFixtures.Email("gone@example.com"), null, true, true, Guid.Parse("c2000000-0000-0000-0000-000000000006"));

        Customer[] all = [ahmed, najd, redSea, faisal, foreign, deleted];
        for (int index = 0; index < all.Length; index++)
        {
            await CustomersTestContext.SeedAt(database, Clock.Later(index + 1), all[index]);
        }

        await using (CustomersDbContext store = CustomersTestContext.Store(database))
        {
            Customer tracked = await store.Customers.FindAsync([deleted.Id], Stubs.Ct) ?? throw new InvalidOperationException("seeded customer missing");
            store.Customers.Remove(tracked);
            await store.SaveChangesAsync(Stubs.Ct);
        }

        return new SeedGraph(database, ahmed, najd, redSea, faisal, foreign, deleted);
    }

    /// <summary>The customers the caller with this scope can read: loaded through the real InMemory context, so the
    /// soft-delete and organisation-scope filters apply exactly as in production. The audit stamps are the real ones.</summary>
    public async Task<Customer[]> Visible(Crm.BuildingBlocks.Application.Abstractions.OrganisationScope scope)
    {
        await using CustomersDbContext context = Context(scope);
        return [.. await context.Customers.AsNoTracking().ToListAsync(Stubs.Ct)];
    }

    /// <summary>A context over the graph database for the caller with this scope.</summary>
    public CustomersDbContext Context(Crm.BuildingBlocks.Application.Abstractions.OrganisationScope scope) =>
        CustomersTestContext.Create(Database, CurrentUsers.Agent(scope));
}
