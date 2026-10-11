using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.Modules.Organisation.Application.Queries.ListDepartmentReferences;
using Crm.Modules.Organisation.Contracts;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Organisation.Tests.Application.Queries.ListDepartmentReferences;

/// <summary>A substituted repository that evaluates the specification in memory (ordering by the OrganisationCode value
/// object cannot run on EF InMemory).</summary>
public sealed class ListDepartmentReferencesQueryHandlerTests
{
    private readonly IRepository<Department, DepartmentId> _repository = Substitute.For<IRepository<Department, DepartmentId>>();
    private readonly Branch _branch = Branches.New(Ids.TenantA, id: Ids.BranchA1);

    private Task<Result<IReadOnlyList<DepartmentReference>>> RunAsync(bool activeOnly)
    {
        _repository.Lists<Department, DepartmentId>(
            Departments.New(_branch, "ZZ-LAST", id: Ids.DeptA1, name: "Last"),
            Departments.New(_branch, "AA-FIRST", id: Ids.DeptA2, name: "First"),
            Departments.New(_branch, "MM-OLD", active: false, id: Ids.DeptA3, name: "Old"));
        return new ListDepartmentReferencesQueryHandler(_repository).Handle(new ListDepartmentReferencesQuery(activeOnly), Stubs.Ct);
    }

    [Fact]
    public async Task AllDepartmentsAreReturnedOrderedByCode()
    {
        Result<IReadOnlyList<DepartmentReference>> result = await RunAsync(activeOnly: false);

        Assert.Equal(["AA-FIRST", "MM-OLD", "ZZ-LAST"], result.Value.Select(d => d.Code));
    }

    [Fact]
    public async Task ActiveOnlyDropsInactiveDepartments()
    {
        Result<IReadOnlyList<DepartmentReference>> result = await RunAsync(activeOnly: true);

        Assert.Equal(["AA-FIRST", "ZZ-LAST"], result.Value.Select(d => d.Code));
    }

    [Fact]
    public async Task MapsEveryField()
    {
        DepartmentReference first = (await RunAsync(activeOnly: false)).Value.Single(d => d.Code == "AA-FIRST");

        Assert.Equal(new DepartmentReference(Ids.DeptA2.Value, Ids.TenantA.Value, Ids.BranchA1.Value, "AA-FIRST", "اسم", "First", true), first);
    }

    [Fact]
    public async Task InactiveDepartmentsAreFlaggedWhenIncluded()
    {
        DepartmentReference old = (await RunAsync(activeOnly: false)).Value.Single(d => d.Code == "MM-OLD");

        Assert.False(old.IsActive);
    }

    [Fact]
    public void TheQueryRequiresAnAuthenticatedUser()
    {
        Assert.True(Stubs.HasAttribute<RequireAuthenticatedUserAttribute>(typeof(ListDepartmentReferencesQuery)));
    }
}
