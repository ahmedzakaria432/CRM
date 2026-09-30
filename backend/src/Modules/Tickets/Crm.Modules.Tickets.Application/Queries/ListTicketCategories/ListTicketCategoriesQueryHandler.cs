using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Tickets.Application.Specifications;
using Crm.Modules.Tickets.Domain;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;
using MediatR;

namespace Crm.Modules.Tickets.Application.Queries.ListTicketCategories;

public sealed class ListTicketCategoriesQueryHandler(IRepository<Category, CategoryId> categories, ICurrentUserService currentUser)
    : IRequestHandler<ListTicketCategoriesQuery, Result<IReadOnlyList<TicketCategoryDto>>>
{
    public async Task<Result<IReadOnlyList<TicketCategoryDto>>> Handle(ListTicketCategoriesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<DepartmentId> departments = request.DepartmentId is { } id ? [new DepartmentId(id)] : currentUser.Scope.Departments;

        IReadOnlyList<Category> found = await categories.ListAsync(
            new CategoriesSpecification(departments, activeOnly: true, customerVisibleOnly: false), cancellationToken);

        return Result.Success<IReadOnlyList<TicketCategoryDto>>([.. found.Select(TicketCategoryDto.FromDomain)]);
    }
}
