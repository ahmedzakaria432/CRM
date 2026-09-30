using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Organisation.Domain;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Organisation.Application.Specifications;

public sealed class MembershipsSpecification : Specification<Membership>
{
    public MembershipsSpecification(Guid? userId, Guid? departmentId)
    {
        DepartmentId? department = departmentId is { } id ? new DepartmentId(id) : null;
        Where(m => (userId == null || m.UserId == userId) && (department == null || m.DepartmentId.Equals(department)));

        AsReadOnly();
    }
}
