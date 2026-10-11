using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.Modules.Organisation.Application;
using Crm.Modules.Organisation.Application.Commands.CreateBranch;
using Crm.Modules.Organisation.Application.Specifications;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Organisation.Tests.Application.Commands.CreateBranch;

public sealed class CreateBranchCommandHandlerTests
{
    private readonly IRepository<Branch, BranchId> _repository = Substitute.For<IRepository<Branch, BranchId>>();
    private readonly IOrganisationUnitOfWork _unitOfWork = Substitute.For<IOrganisationUnitOfWork>();

    private CreateBranchCommandHandler Handler(ICurrentUserService? user = null) =>
        new(_repository, _unitOfWork, user ?? CurrentUsers.Admin());

    [Fact]
    public async Task SuccessCreatesTheBranchInTheCallersTenantWithAnUpperCaseCode()
    {
        _repository.CountsAs<Branch, BranchId>(0);

        Result<BranchDto> result = await Handler().Handle(new CreateBranchCommand("ryd", "الرياض", "Riyadh"), Stubs.Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("RYD", result.Value.Code);
        Assert.Equal("Riyadh", result.Value.Name.En);
        Assert.Equal("الرياض", result.Value.Name.Ar);
        Assert.True(result.Value.IsActive);
        _repository.Received(1).Add(Arg.Is<Branch>(b => b.TenantId == Ids.TenantA && b.Code.Value == "RYD"));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ANoneTenantScopeIsForbiddenBeforeAnyRepositoryCall()
    {
        ICurrentUserService noTenant = CurrentUsers.Agent(OrganisationScope.None);

        Result<BranchDto> result = await Handler(noTenant).Handle(new CreateBranchCommand("RYD", "ر", "R"), Stubs.Ct);

        Expect.Failure(result, "Organisation.TenantRequired", ErrorType.Forbidden);
        Assert.Empty(_repository.ReceivedCalls());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ADuplicateCodeIsAConflictNamingTheNormalisedCode()
    {
        _repository.CountsAs<Branch, BranchId>(1);

        Result<BranchDto> result = await Handler().Handle(new CreateBranchCommand(" ryd ", "ر", "R"), Stubs.Ct);

        Expect.Failure(result, "Organisation.BranchCodeTaken", ErrorType.Conflict);
        Assert.Contains("'RYD'", result.Error.Description, StringComparison.Ordinal);
        _repository.DidNotReceive().Add(Arg.Any<Branch>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheDuplicateCheckUsesTheBranchByCodeSpecification()
    {
        _repository.CountsAs<Branch, BranchId>(0);

        await Handler().Handle(new CreateBranchCommand("ryd", "ر", "R"), Stubs.Ct);

        await _repository.Received(1).CountAsync(
            Arg.Is<Crm.BuildingBlocks.Application.Specifications.Specification<Branch>>(s => s is BranchByCodeSpecification),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnInvalidCodeReachingTheHandlerThrows()
    {
        await Expect.DomainCode(() => Handler().Handle(new CreateBranchCommand("a b", "ر", "R"), Stubs.Ct), "Organisation.CodeInvalid");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ABlankNameReachingTheHandlerThrows()
    {
        _repository.CountsAs<Branch, BranchId>(0);

        await Expect.DomainCode(() => Handler().Handle(new CreateBranchCommand("RYD", " ", "R"), Stubs.Ct), "Organisation.NameRequired");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TheCommandRequiresTheAdminOrganisationPermission()
    {
        Assert.Equal(CrmPermissions.AdminOrganisation, Stubs.PermissionOf(typeof(CreateBranchCommand)));
    }
}
