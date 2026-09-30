using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Tickets.Application.Specifications;
using Crm.Modules.Tickets.Domain;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;
using MediatR;

namespace Crm.Modules.Tickets.Application.Queries.ListCategories;

public sealed class ListCategoriesQueryHandler(IRepository<Category, CategoryId> categories)
    : IRequestHandler<ListCategoriesQuery, Result<IReadOnlyList<TicketCategoryDto>>>
{
    public async Task<Result<IReadOnlyList<TicketCategoryDto>>> Handle(ListCategoriesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<DepartmentId>? departments = request.DepartmentId is { } id ? [new DepartmentId(id)] : null;

        IReadOnlyList<Category> found = await categories.ListAsync(
            new CategoriesSpecification(departments, request.ActiveOnly, customerVisibleOnly: false), cancellationToken);

        return Result.Success<IReadOnlyList<TicketCategoryDto>>([.. found.Select(TicketCategoryDto.FromDomain)]);
    }
}
