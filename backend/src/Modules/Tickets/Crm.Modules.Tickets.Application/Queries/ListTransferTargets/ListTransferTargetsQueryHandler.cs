using Crm.Modules.Organisation.Contracts;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Queries.ListTransferTargets;

public sealed class ListTransferTargetsQueryHandler(IOrganisationModuleApi organisation)
    : IRequestHandler<ListTransferTargetsQuery, Result<IReadOnlyList<TicketDepartmentRefDto>>>
{
    public async Task<Result<IReadOnlyList<TicketDepartmentRefDto>>> Handle(ListTransferTargetsQuery request, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<DepartmentReference>> result = await organisation.ListDepartmentsAsync(activeOnly: true, cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<IReadOnlyList<TicketDepartmentRefDto>>(result.Error);
        }

        return Result.Success<IReadOnlyList<TicketDepartmentRefDto>>(
            [.. result.Value.Select(d => new TicketDepartmentRefDto(d.Id, d.Code, d.NameAr, d.NameEn))]);
    }
}
