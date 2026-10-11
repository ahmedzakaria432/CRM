using Crm.Modules.Organisation.Contracts;
using Crm.Modules.Tickets.Application.Queries.ListTransferTargets;
using Crm.Modules.Tickets.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Tickets.Tests.Application.Queries;

public sealed class ListTransferTargetsQueryHandlerTests
{
    private readonly Harness _h = new();

    [Fact]
    public async Task OnlyActiveDepartmentsAreRequested()
    {
        _h.Organisation.ListDepartmentsAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success<IReadOnlyList<DepartmentReference>>([])));

        await new ListTransferTargetsQueryHandler(_h.Organisation).Handle(new ListTransferTargetsQuery(), Stubs.Ct);

        await _h.Organisation.Received(1).ListDepartmentsAsync(true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AFailureIsPassedThrough()
    {
        _h.Organisation.ListDepartmentsAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Failure<IReadOnlyList<DepartmentReference>>(Error.Forbidden("Organisation.OutOfScope", "x"))));

        Result<IReadOnlyList<TicketDepartmentRefDto>> result = await new ListTransferTargetsQueryHandler(_h.Organisation).Handle(new ListTransferTargetsQuery(), Stubs.Ct);

        Expect.Failure(result, ErrorCodes.OutOfScope, ErrorType.Forbidden);
    }

    [Fact]
    public async Task DepartmentsAreMappedToCodeAndBothNames()
    {
        _h.Organisation.ListDepartmentsAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success<IReadOnlyList<DepartmentReference>>(
            [
                TicketFixtures.Department(Ids.DepartmentA1, code: "RYD-CARE", nameAr: "الرعاية", nameEn: "Care"),
                TicketFixtures.Department(Ids.DepartmentA2, code: "RYD-BILL", nameAr: "الفواتير", nameEn: "Billing"),
            ])));

        Result<IReadOnlyList<TicketDepartmentRefDto>> result = await new ListTransferTargetsQueryHandler(_h.Organisation).Handle(new ListTransferTargetsQuery(), Stubs.Ct);

        Expect.Success(result);
        Assert.Equal(
            [
                new TicketDepartmentRefDto(Ids.DepartmentA1.Value, "RYD-CARE", "الرعاية", "Care"),
                new TicketDepartmentRefDto(Ids.DepartmentA2.Value, "RYD-BILL", "الفواتير", "Billing"),
            ],
            result.Value);
    }
}
