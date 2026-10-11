using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Identity.Application;
using Crm.Modules.Identity.Application.Abstractions;
using Crm.Modules.Identity.Application.Commands.CreateUser;
using Crm.Modules.Identity.Application.Commands.DeactivateUser;
using Crm.Modules.Identity.Application.Commands.ReactivateUser;
using Crm.Modules.Identity.Application.Commands.ResetUserPassword;
using Crm.Modules.Identity.Application.Commands.UpdateUserProfile;
using Crm.Modules.Identity.Application.Specifications;
using Crm.Modules.Identity.Domain;
using Crm.Modules.Identity.Tests.Support;
using Crm.SharedKernel;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Identity.Tests.Application.Commands;

public sealed class CreateUserCommandHandlerTests
{
    private readonly IRepository<User, UserId> _users = Substitute.For<IRepository<User, UserId>>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IIdentityUnitOfWork _unitOfWork = Substitute.For<IIdentityUnitOfWork>();
    private readonly List<User> _added = [];

    public CreateUserCommandHandlerTests()
    {
        _hasher.Hash("initial-pass-1").Returns("hash-initial");
        _users.CountsAs(0);
        _users.When(r => r.Add(Arg.Any<User>())).Do(call => _added.Add(call.Arg<User>()));
    }

    private Task<Result<UserDto>> Run(ICurrentUserService? caller = null, string email = " New@Example.COM ", string name = " New User ", string locale = "ar") =>
        new CreateUserCommandHandler(_users, _hasher, caller ?? CurrentUsers.Admin(), Clock.Fixed(), _unitOfWork)
            .Handle(new CreateUserCommand(email, name, locale, "initial-pass-1"), Stubs.Ct);

    [Fact]
    public async Task TheNoneTenantIsForbiddenWithNoRepositoryCall()
    {
        Result<UserDto> result = await Run(CurrentUsers.Anonymous());

        Expect.Failure(result, "Identity.TenantRequired", ErrorType.Forbidden);
        await _users.DidNotReceiveWithAnyArgs().CountAsync(Arg.Any<Specification<User>>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ATakenEmailIsAConflictWithNoAddAndNoSave()
    {
        _users.CountsAs(1);

        Result<UserDto> result = await Run();

        Expect.Failure(result, "Identity.EmailTaken", ErrorType.Conflict);
        Assert.Empty(_added);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmailUniquenessIsGlobalSoTheSpecificationCarriesNoTenant()
    {
        await Run();

        await _users.Received(1).CountAsync(Arg.Is<Specification<User>>(s => s is UserByEmailSpecification), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SuccessAddsTheUserForTheCallersTenantNormalisedAndSavesOnce()
    {
        Result<UserDto> result = await Run(CurrentUsers.Admin(Ids.TenantB));

        Assert.True(result.IsSuccess);
        User user = Assert.Single(_added);
        Assert.Equal(Ids.TenantB, user.TenantId);
        Assert.Equal("new@example.com", user.Email.Value);
        Assert.Equal("New User", user.DisplayName.Value);
        Assert.Same(PreferredLocale.Arabic, user.PreferredLocale);
        Assert.Equal("hash-initial", user.PasswordHash);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SuccessReturnsTheMappedDto()
    {
        Result<UserDto> result = await Run();

        UserDto dto = result.Value;
        User user = Assert.Single(_added);
        Assert.Equal(user.Id.Value, dto.Id);
        Assert.Equal("new@example.com", dto.Email);
        Assert.Equal("New User", dto.DisplayName);
        Assert.Equal("ar", dto.PreferredLocale);
        Assert.True(dto.IsActive);
        Assert.False(dto.IsLockedOut);
        Assert.Null(dto.LastSignInAtUtc);
        Assert.Empty(dto.RowVersion);
    }

    [Fact]
    public async Task AMalformedEmailThatPassedTheValidatorIsADomainException() =>
        await Expect.DomainCode(() => Run(email: "a@b"), "Identity.EmailInvalid");

    [Fact]
    public async Task AnUnsupportedLocaleReachingTheHandlerIsADomainException() =>
        await Expect.DomainCode(() => Run(locale: "fr"), "Identity.LocaleUnsupported");

    [Fact]
    public async Task ABlankNameReachingTheHandlerIsADomainException() =>
        await Expect.DomainCode(() => Run(name: "  "), "Identity.DisplayNameRequired");
}

public sealed class UpdateUserProfileCommandHandlerTests
{
    private readonly IRepository<User, UserId> _users = Substitute.For<IRepository<User, UserId>>();
    private readonly IIdentityUnitOfWork _unitOfWork = Substitute.For<IIdentityUnitOfWork>();

    private Task<Result<UserDto>> Run(string name = "Renamed", string locale = "ar", byte[]? rowVersion = null) =>
        new UpdateUserProfileCommandHandler(_users, CurrentUsers.Admin(), Clock.Fixed(), _unitOfWork)
            .Handle(new UpdateUserProfileCommand(Ids.User, name, locale, rowVersion ?? Ids.RowVersion()), Stubs.Ct);

    [Fact]
    public async Task AMissingUserIsNotFoundNamingTheId()
    {
        Result<UserDto> result = await Run();

        Expect.Failure(result, "Identity.UserNotFound", ErrorType.NotFound);
        Assert.Contains(Ids.User.ToString(), result.Error.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AUserOfAnotherTenantIsForbiddenWithNoRowVersionAndNoSave()
    {
        User other = Users.New(Ids.TenantB);
        _users.Holds(other);

        Result<UserDto> result = await Run();

        Expect.Failure(result, "Organisation.OutOfScope", ErrorType.Forbidden);
        _users.DidNotReceive().SetOriginalRowVersion(Arg.Any<User>(), Arg.Any<byte[]>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        Assert.Equal("Omar Haddad", other.DisplayName.Value);
    }

    [Fact]
    public async Task SuccessAppliesTheRowVersionBeforeSavingThenReplacesNameAndLocale()
    {
        User user = Users.New();
        _users.Holds(user);
        byte[] rowVersion = Ids.OtherRowVersion();

        Result<UserDto> result = await Run(rowVersion: rowVersion);

        Assert.True(result.IsSuccess);
        Assert.Equal("Renamed", result.Value.DisplayName);
        Assert.Equal("ar", result.Value.PreferredLocale);
        Received.InOrder(() =>
        {
            _users.SetOriginalRowVersion(user, rowVersion);
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task AnUnsupportedLocaleIsADomainException()
    {
        _users.Holds(Users.New());

        await Expect.DomainCode(() => Run(locale: "fr"), "Identity.LocaleUnsupported");
    }

    [Fact]
    public async Task ABlankNameIsADomainException()
    {
        _users.Holds(Users.New());

        await Expect.DomainCode(() => Run(name: " "), "Identity.DisplayNameRequired");
    }
}

public sealed class DeactivateUserCommandHandlerTests
{
    private readonly IRepository<User, UserId> _users = Substitute.For<IRepository<User, UserId>>();
    private readonly IIdentityUnitOfWork _unitOfWork = Substitute.For<IIdentityUnitOfWork>();

    private Task<Result<UserDto>> Run(ICurrentUserService? caller = null) =>
        new DeactivateUserCommandHandler(_users, caller ?? CurrentUsers.Admin(), Clock.Fixed(), _unitOfWork)
            .Handle(new DeactivateUserCommand(Ids.User, Ids.RowVersion()), Stubs.Ct);

    private static ICurrentUserService OtherAdmin() =>
        CurrentUsers.WithIds(CurrentUsers.Admin().Scope, Ids.OtherUser.ToString(), null);

    [Fact]
    public async Task AMissingUserIsNotFound()
    {
        Result<UserDto> result = await Run(OtherAdmin());

        Expect.Failure(result, "Identity.UserNotFound", ErrorType.NotFound);
    }

    [Fact]
    public async Task AUserOfAnotherTenantIsForbidden()
    {
        _users.Holds(Users.New(Ids.TenantB));

        Result<UserDto> result = await Run(OtherAdmin());

        Expect.Failure(result, "Organisation.OutOfScope", ErrorType.Forbidden);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeactivatingYourselfIsADomainFailureWithNoSave()
    {
        _users.Holds(Users.New());

        Result<UserDto> result = await Run();

        Expect.Failure(result, "Identity.CannotDeactivateSelf", ErrorType.Domain);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        _users.DidNotReceive().SetOriginalRowVersion(Arg.Any<User>(), Arg.Any<byte[]>());
    }

    [Fact]
    public async Task TheSelfCheckIsCaseInsensitive()
    {
        _users.Holds(Users.New());
        ICurrentUserService upper = CurrentUsers.WithIds(CurrentUsers.Admin().Scope, Ids.User.ToString().ToUpperInvariant(), null);

        Result<UserDto> result = await Run(upper);

        Expect.Failure(result, "Identity.CannotDeactivateSelf", ErrorType.Domain);
    }

    [Fact]
    public async Task TheTenantCheckComesBeforeTheSelfCheck()
    {
        _users.Holds(Users.New(Ids.TenantB));

        Result<UserDto> result = await Run();

        Expect.Failure(result, "Organisation.OutOfScope", ErrorType.Forbidden);
    }

    [Fact]
    public async Task ANullCallerUserIdIsNotSelf()
    {
        _users.Holds(Users.New());

        Result<UserDto> result = await Run(CurrentUsers.WithIds(CurrentUsers.Admin().Scope, null, null));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task AnAlreadyInactiveUserIsADomainExceptionWithNoSave()
    {
        _users.Holds(Users.Inactive());

        await Expect.DomainCode(() => Run(OtherAdmin()), "Identity.UserAlreadyInactive");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SuccessAppliesTheRowVersionFirstRaisesTheEventAndSavesOnce()
    {
        User user = Users.New();
        _users.Holds(user);

        Result<UserDto> result = await Run(OtherAdmin());

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsActive);
        Assert.IsType<UserDeactivated>(Assert.Single(user.DomainEvents));
        Received.InOrder(() =>
        {
            _users.SetOriginalRowVersion(user, Arg.Any<byte[]>());
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}

public sealed class ReactivateUserCommandHandlerTests
{
    private readonly IRepository<User, UserId> _users = Substitute.For<IRepository<User, UserId>>();
    private readonly IIdentityUnitOfWork _unitOfWork = Substitute.For<IIdentityUnitOfWork>();

    private Task<Result<UserDto>> Run() =>
        new ReactivateUserCommandHandler(_users, CurrentUsers.Admin(), Clock.Fixed(), _unitOfWork)
            .Handle(new ReactivateUserCommand(Ids.User, Ids.RowVersion()), Stubs.Ct);

    [Fact]
    public async Task AMissingUserIsNotFound() => Expect.Failure(await Run(), "Identity.UserNotFound", ErrorType.NotFound);

    [Fact]
    public async Task AUserOfAnotherTenantIsForbidden()
    {
        _users.Holds(Users.Inactive(Ids.TenantB));

        Expect.Failure(await Run(), "Organisation.OutOfScope", ErrorType.Forbidden);
    }

    [Fact]
    public async Task AnActiveUserIsADomainException()
    {
        _users.Holds(Users.New());

        await Expect.DomainCode(Run, "Identity.UserAlreadyActive");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SuccessClearsTheLockoutAppliesTheRowVersionAndSavesOnce()
    {
        User user = Users.Failed(3);
        user.Deactivate(Clock.Now);
        _users.Holds(user);

        Result<UserDto> result = await Run();

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsActive);
        Assert.False(result.Value.IsLockedOut);
        Assert.Equal(0, user.FailedSignInCount);
        Received.InOrder(() =>
        {
            _users.SetOriginalRowVersion(user, Arg.Any<byte[]>());
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }
}

public sealed class ResetUserPasswordCommandHandlerTests
{
    private readonly IRepository<User, UserId> _users = Substitute.For<IRepository<User, UserId>>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IIdentityUnitOfWork _unitOfWork = Substitute.For<IIdentityUnitOfWork>();

    public ResetUserPasswordCommandHandlerTests() => _hasher.Hash("new-pass-1234").Returns("hash-new");

    private Task<Result<UserDto>> Run() =>
        new ResetUserPasswordCommandHandler(_users, _hasher, CurrentUsers.Admin(), Clock.Fixed(), _unitOfWork)
            .Handle(new ResetUserPasswordCommand(Ids.User, "new-pass-1234", Ids.RowVersion()), Stubs.Ct);

    [Fact]
    public async Task AMissingUserIsNotFound() => Expect.Failure(await Run(), "Identity.UserNotFound", ErrorType.NotFound);

    [Fact]
    public async Task AUserOfAnotherTenantIsForbidden()
    {
        _users.Holds(Users.New(Ids.TenantB));

        Expect.Failure(await Run(), "Organisation.OutOfScope", ErrorType.Forbidden);
        _hasher.DidNotReceive().Hash(Arg.Any<string>());
    }

    [Fact]
    public async Task SuccessReplacesTheHashClearsTheLockoutAndRaisesTheEventWithoutAKeptSession()
    {
        User user = Users.Locked(Clock.Now.AddMinutes(5));
        _users.Holds(user);

        Result<UserDto> result = await Run();

        Assert.True(result.IsSuccess);
        Assert.Equal("hash-new", user.PasswordHash);
        Assert.False(result.Value.IsLockedOut);
        Assert.Null(Assert.IsType<UserPasswordChanged>(Assert.Single(user.DomainEvents)).KeepSessionId);
        Assert.Equal(Clock.Now, Assert.IsType<UserPasswordChanged>(Assert.Single(user.DomainEvents)).OccurredOnUtc);
        Received.InOrder(() =>
        {
            _users.SetOriginalRowVersion(user, Arg.Any<byte[]>());
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
