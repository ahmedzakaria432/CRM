using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.Modules.Organisation.Application.Queries.GetDepartmentReference;
using Crm.Modules.Organisation.Contracts;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Organisation.Tests.Application.Queries.GetDepartmentReference;

public sealed class GetDepartmentReferenceQueryHandlerTests
{
    private readonly IRepository<Department, DepartmentId> _repository = Substitute.For<IRepository<Department, DepartmentId>>();

    [Fact]
    public async Task AFoundDepartmentMapsAllSevenFields()
    {
        Department department = Departments.New(Branches.New(Ids.TenantA, id: Ids.BranchA1), "care", id: Ids.DeptA1, name: "Care");
        _repository.Holds<Department, DepartmentId>(department);

        Result<DepartmentReference> result = await new GetDepartmentReferenceQueryHandler(_repository)
            .Handle(new GetDepartmentReferenceQuery(Ids.DeptA1.Value), Stubs.Ct);

        Assert.Equal(new DepartmentReference(Ids.DeptA1.Value, Ids.TenantA.Value, Ids.BranchA1.Value, "CARE", "اسم", "Care", true), result.Value);
    }

    [Fact]
    public async Task AnInactiveDepartmentIsStillReturnedAsInactive()
    {
        Department department = Departments.New(Branches.New(), "old", active: false, id: Ids.DeptA1);
        _repository.Holds<Department, DepartmentId>(department);

        Result<DepartmentReference> result = await new GetDepartmentReferenceQueryHandler(_repository)
            .Handle(new GetDepartmentReferenceQuery(Ids.DeptA1.Value), Stubs.Ct);

        Assert.False(result.Value.IsActive);
    }

    [Fact]
    public async Task AMissingDepartmentIsNotFound()
    {
        var id = Guid.NewGuid();

        Result<DepartmentReference> result = await new GetDepartmentReferenceQueryHandler(_repository)
            .Handle(new GetDepartmentReferenceQuery(id), Stubs.Ct);

        Expect.Failure(result, "Organisation.DepartmentNotFound", ErrorType.NotFound);
        Assert.Contains(id.ToString(), result.Error.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void TheQueryRequiresAnAuthenticatedUser()
    {
        Assert.True(Stubs.HasAttribute<RequireAuthenticatedUserAttribute>(typeof(GetDepartmentReferenceQuery)));
    }
}
