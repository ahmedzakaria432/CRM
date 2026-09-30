using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Organisation.Application.Specifications;
using Crm.Modules.Organisation.Domain;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;
using MediatR;

namespace Crm.Modules.Organisation.Application.Commands.AddMembership;

public sealed class AddMembershipCommandHandler(
    IRepository<Department, DepartmentId> departmentRepository,
    IRepository<Membership, MembershipId> membershipRepository,
    IOrganisationUnitOfWork unitOfWork)
    : IRequestHandler<AddMembershipCommand, Result<MembershipDto>>
{
    public async Task<Result<MembershipDto>> Handle(AddMembershipCommand request, CancellationToken cancellationToken)
    {
        var departmentId = new DepartmentId(request.DepartmentId);
        Department? department = await departmentRepository.GetByIdAsync(departmentId, cancellationToken);

        if (department is null)
        {
            return Result.Failure<MembershipDto>(Error.NotFound("Organisation.DepartmentNotFound", $"Department '{request.DepartmentId}' was not found."));
        }

        int existing = await membershipRepository.CountAsync(new ActiveMembershipSpecification(request.UserId, departmentId), cancellationToken);
        if (existing > 0)
        {
            return Result.Failure<MembershipDto>(Error.Conflict("Organisation.MembershipExists", "The user already belongs to this department."));
        }

        var role = Enum.Parse<SystemRole>(request.Role, false);
        Membership membership = Membership.Create(department, request.UserId, role);
        membershipRepository.Add(membership);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(MembershipDto.FromDomain(membership));
    }
}
