using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Queries.ListDepartments;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Organisation.Tests.Application.Queries.ListDepartments;

/// <summary>Substituted repositories that evaluate the specification in memory (ordering by the OrganisationCode value
/// object cannot run on EF InMemory). The tenant filter is asserted in OrganisationDbContextTests.</summary>
public sealed class ListDepartmentsQueryHandlerTests
{
    private readonly IRepository<Department, DepartmentId> _departments = Substitute.For<IRepository<Department, DepartmentId>>();
    private readonly IRepository<Branch, BranchId> _branches = Substitute.For<IRepository<Branch, BranchId>>();
    private readonly Branch _riyadh = Branches.New(Ids.TenantA, "RYD", id: Ids.BranchA1);
    private readonly Branch _jeddah = Branches.New(Ids.TenantA, "JED", id: Ids.BranchA2);
    private readonly Department _care;
    private readonly Department _billing;
    private readonly Department _jeddahCare;
    private readonly Department _retired;

    public ListDepartmentsQueryHandlerTests()
    {
        _care = Departments.New(_riyadh, "RYD-CARE", id: Ids.DeptA1);
        _billing = Departments.New(_riyadh, "RYD-BILL", id: Ids.DeptA2);
        _jeddahCare = Departments.New(_jeddah, "JED-CARE", id: Ids.DeptA3);
        _retired = Departments.New(_riyadh, "RYD-OLD", active: false);
    }

    private async Task<Result<IReadOnlyList<DepartmentDto>>> RunAsync(Guid? branchId, bool activeOnly, bool jeddahActive = true)
    {
        if (!jeddahActive)
        {
            _jeddah.Deactivate();
        }

        _departments.Lists<Department, DepartmentId>(_care, _billing, _jeddahCare, _retired);
        _branches.Lists<Branch, BranchId>(_riyadh, _jeddah);
        return await new ListDepartmentsQueryHandler(_departments, _branches).Handle(new ListDepartmentsQuery(branchId, activeOnly), Stubs.Ct);
    }

    [Fact]
    public async Task NoFilterReturnsEveryDepartmentOrderedByCode()
    {
        Result<IReadOnlyList<DepartmentDto>> result = await RunAsync(null, activeOnly: false);

        Assert.Equal(["JED-CARE", "RYD-BILL", "RYD-CARE", "RYD-OLD"], result.Value.Select(d => d.Code));
    }

    [Fact]
    public async Task TheBranchFilterKeepsOnlyThatBranchesDepartments()
    {
        Result<IReadOnlyList<DepartmentDto>> result = await RunAsync(Ids.BranchA2.Value, activeOnly: false);

        Assert.Equal(["JED-CARE"], result.Value.Select(d => d.Code));
        Assert.All(result.Value, d => Assert.Equal(Ids.BranchA2.Value, d.BranchId));
    }

    [Fact]
    public async Task ActiveOnlyDropsInactiveDepartments()
    {
        Result<IReadOnlyList<DepartmentDto>> result = await RunAsync(null, activeOnly: true);

        Assert.Equal(["JED-CARE", "RYD-BILL", "RYD-CARE"], result.Value.Select(d => d.Code));
    }

    [Fact]
    public async Task ActiveOnlyAlsoDropsDepartmentsOfInactiveBranches()
    {
        Result<IReadOnlyList<DepartmentDto>> result = await RunAsync(null, activeOnly: true, jeddahActive: false);

        Assert.Equal(["RYD-BILL", "RYD-CARE"], result.Value.Select(d => d.Code));
    }

    [Fact]
    public async Task WithoutActiveOnlyDepartmentsOfInactiveBranchesAreKept()
    {
        Result<IReadOnlyList<DepartmentDto>> result = await RunAsync(null, activeOnly: false, jeddahActive: false);

        Assert.Contains("JED-CARE", result.Value.Select(d => d.Code));
    }

    [Fact]
    public async Task ActiveOnlyDoesNotQueryBranchesWhenNotRequested()
    {
        await RunAsync(null, activeOnly: false);

        await _branches.DidNotReceive().ListAsync(Arg.Any<Crm.BuildingBlocks.Application.Specifications.Specification<Branch>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheBranchFilterAndActiveOnlyCombine()
    {
        Result<IReadOnlyList<DepartmentDto>> result = await RunAsync(Ids.BranchA1.Value, activeOnly: true);

        Assert.Equal(["RYD-BILL", "RYD-CARE"], result.Value.Select(d => d.Code));
    }

    [Fact]
    public async Task AnInactiveBranchFilteredByIdWithActiveOnlyYieldsNothing()
    {
        Result<IReadOnlyList<DepartmentDto>> result = await RunAsync(Ids.BranchA2.Value, activeOnly: true, jeddahActive: false);

        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task MapsEveryField()
    {
        DepartmentDto care = (await RunAsync(null, activeOnly: false)).Value.Single(d => d.Code == "RYD-CARE");

        Assert.Equal(Ids.DeptA1.Value, care.Id);
        Assert.Equal(Ids.BranchA1.Value, care.BranchId);
        Assert.Equal("Care", care.Name.En);
        Assert.True(care.IsActive);
    }

    [Fact]
    public void TheQueryRequiresTheAdminOrganisationPermission()
    {
        Assert.Equal(CrmPermissions.AdminOrganisation, Stubs.PermissionOf(typeof(ListDepartmentsQuery)));
    }
}
