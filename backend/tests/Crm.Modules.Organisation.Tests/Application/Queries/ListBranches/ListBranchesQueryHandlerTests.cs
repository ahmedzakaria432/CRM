using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Queries.ListBranches;
using Crm.Modules.Organisation.Application.Specifications;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Organisation.Tests.Application.Queries.ListBranches;

/// <summary>The handler runs over a substituted repository that evaluates the specification in memory: ordering by the
/// OrganisationCode value object cannot run on the EF InMemory provider (it is not IComparable). The tenant filter is
/// asserted in OrganisationDbContextTests.</summary>
public sealed class ListBranchesQueryHandlerTests
{
    private readonly IRepository<Branch, BranchId> _repository = Substitute.For<IRepository<Branch, BranchId>>();

    private readonly Branch[] _branches =
    [
        Branches.New(Ids.TenantA, "JED", name: "Jeddah"),
        Branches.New(Ids.TenantA, "RYD", name: "Riyadh"),
        Branches.New(Ids.TenantA, "DMM", active: false, name: "Dammam"),
    ];

    private async Task<Result<IReadOnlyList<BranchDto>>> RunAsync(bool activeOnly)
    {
        _repository.Lists<Branch, BranchId>(_branches);
        return await new ListBranchesQueryHandler(_repository).Handle(new ListBranchesQuery(activeOnly), Stubs.Ct);
    }

    [Fact]
    public async Task ReturnsEveryBranchOrderedByCode()
    {
        Result<IReadOnlyList<BranchDto>> result = await RunAsync(activeOnly: false);

        Assert.Equal(["DMM", "JED", "RYD"], result.Value.Select(b => b.Code));
    }

    [Fact]
    public async Task ActiveOnlyDropsInactiveBranches()
    {
        Result<IReadOnlyList<BranchDto>> result = await RunAsync(activeOnly: true);

        Assert.Equal(["JED", "RYD"], result.Value.Select(b => b.Code));
        Assert.All(result.Value, b => Assert.True(b.IsActive));
    }

    [Fact]
    public async Task TheHandlerAsksForTheBranchesSpecificationWithTheFlag()
    {
        await RunAsync(activeOnly: true);

        await _repository.Received(1).ListAsync(
            Arg.Is<Crm.BuildingBlocks.Application.Specifications.Specification<Branch>>(s => s is BranchesSpecification),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NoBranchesIsAnEmptySuccess()
    {
        _repository.Lists<Branch, BranchId>();

        Result<IReadOnlyList<BranchDto>> result = await new ListBranchesQueryHandler(_repository).Handle(new ListBranchesQuery(false), Stubs.Ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task MapsEveryField()
    {
        BranchDto jeddah = (await RunAsync(activeOnly: false)).Value.Single(b => b.Code == "JED");

        Assert.Equal("Jeddah", jeddah.Name.En);
        Assert.Equal("اسم", jeddah.Name.Ar);
        Assert.True(jeddah.IsActive);
        Assert.NotEqual(Guid.Empty, jeddah.Id);
    }

    [Fact]
    public void TheQueryRequiresTheAdminOrganisationPermission()
    {
        Assert.Equal(CrmPermissions.AdminOrganisation, Stubs.PermissionOf(typeof(ListBranchesQuery)));
    }
}
