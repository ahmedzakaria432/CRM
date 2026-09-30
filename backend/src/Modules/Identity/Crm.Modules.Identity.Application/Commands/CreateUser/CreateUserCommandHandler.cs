using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Identity.Application.Abstractions;
using Crm.Modules.Identity.Application.Specifications;
using Crm.Modules.Identity.Domain;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;
using MediatR;

namespace Crm.Modules.Identity.Application.Commands.CreateUser;

public sealed class CreateUserCommandHandler(
    IRepository<User, UserId> users,
    IPasswordHasher hasher,
    ICurrentUserService currentUser,
    IDateTime dateTime,
    IIdentityUnitOfWork unitOfWork)
    : IRequestHandler<CreateUserCommand, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        TenantId tenant = currentUser.Scope.Tenant;

        if (tenant == TenantId.None)
        {
            return Result.Failure<UserDto>(Error.Forbidden("Identity.TenantRequired", "A user must be created inside a tenant."));
        }

        EmailAddress email = EmailAddress.Create(request.Email);

        if (await users.CountAsync(new UserByEmailSpecification(email), cancellationToken) > 0)
        {
            return Result.Failure<UserDto>(Error.Conflict("Identity.EmailTaken", "A user with this email already exists."));
        }

        var user = User.Create(
            tenant,
            email,
            DisplayName.Create(request.DisplayName),
            PreferredLocale.Create(request.PreferredLocale),
            hasher.Hash(request.InitialPassword));

        users.Add(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(UserDto.FromDomain(user, dateTime.UtcNow));
    }
}
