using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Tickets.Application.Specifications;
using Crm.Modules.Tickets.Domain;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Tickets.Application.Queries.ListPortalTicketCategories;

public sealed class ListPortalTicketCategoriesQueryHandler(IRepository<Category, CategoryId> categories, ICurrentUserService currentUser)
    : IRequestHandler<ListPortalTicketCategoriesQuery, Result<IReadOnlyList<PortalTicketCategoryDto>>>
{
    public async Task<Result<IReadOnlyList<PortalTicketCategoryDto>>> Handle(
        ListPortalTicketCategoriesQuery request, CancellationToken cancellationToken)
    {
        if (PortalCaller.Grant(currentUser) is not { } grant)
        {
            return Result.Failure<IReadOnlyList<PortalTicketCategoryDto>>(TicketsErrors.PortalOnly());
        }

        IReadOnlyList<Category> found = await categories.ListAsync(
            new CategoriesSpecification([grant.HomeDepartmentId], activeOnly: true, customerVisibleOnly: true), cancellationToken);

        return Result.Success<IReadOnlyList<PortalTicketCategoryDto>>(
            [.. found.Select(c => new PortalTicketCategoryDto(c.Id.Value, c.Name.Arabic, c.Name.English))]);
    }
}
