using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Infrastructure.Persistence;

namespace Crm.Modules.Organisation.Tests.Support;

/// <summary>The standard graph in an InMemory store: tenant A has branches RYD and JED (JED inactive when asked) with
/// departments CARE and BILL in RYD and CARE2 in JED; tenant B has one branch and one department.</summary>
public sealed record SeedData(
    Branch Riyadh,
    Branch Jeddah,
    Department RiyadhCare,
    Department RiyadhBilling,
    Department JeddahCare,
    Branch OtherBranch,
    Department OtherDepartment)
{
    public static async Task<SeedData> StandardAsync(string database, bool jeddahInactive = false)
    {
        await using OrganisationDbContext store = OrganisationTestContext.Store(database);

        Branch riyadh = Branches.New(Ids.TenantA, "RYD", id: Ids.BranchA1, name: "Riyadh");
        Branch jeddah = Branches.New(Ids.TenantA, "JED", id: Ids.BranchA2, name: "Jeddah");
        Department care = Departments.New(riyadh, "RYD-CARE", id: Ids.DeptA1, name: "Care Riyadh");
        Department billing = Departments.New(riyadh, "RYD-BILL", id: Ids.DeptA2, name: "Billing Riyadh");
        Department jeddahCare = Departments.New(jeddah, "JED-CARE", id: Ids.DeptA3, name: "Care Jeddah");
        Branch other = Branches.New(Ids.TenantB, "OTH", id: Ids.BranchB1, name: "Other");
        Department otherDepartment = Departments.New(other, "OTH-CARE", id: Ids.DeptB1, name: "Other care");

        if (jeddahInactive)
        {
            jeddah.Deactivate();
        }

        store.Branches.AddRange(riyadh, jeddah, other);
        store.Departments.AddRange(care, billing, jeddahCare, otherDepartment);
        await store.SaveChangesAsync(Stubs.Ct);

        return new SeedData(riyadh, jeddah, care, billing, jeddahCare, other, otherDepartment);
    }
}
