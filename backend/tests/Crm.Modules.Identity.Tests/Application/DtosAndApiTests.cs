using Crm.Modules.Identity.Application;
using Crm.Modules.Identity.Application.Queries.GetStaffUsers;
using Crm.Modules.Identity.Contracts;
using Crm.Modules.Identity.Domain;
using Crm.Modules.Identity.Tests.Support;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Identity.Tests.Application;

public sealed class UserDtoTests
{
    [Fact]
    public void FromDomainMapsEveryField()
    {
        User user = Users.New(locale: "ar");
        user.RecordSuccessfulSignIn(Clock.Now.AddDays(-1));

        UserDto dto = UserDto.FromDomain(user, Clock.Now);

        Assert.Equal(Ids.User, dto.Id);
        Assert.Equal("agent@example.com", dto.Email);
        Assert.Equal("Omar Haddad", dto.DisplayName);
        Assert.Equal("ar", dto.PreferredLocale);
        Assert.True(dto.IsActive);
        Assert.False(dto.IsLockedOut);
        Assert.Equal(Clock.Now.AddDays(-1), dto.LastSignInAtUtc);
        Assert.Null(dto.CreatedAtUtc);
        Assert.Empty(dto.RowVersion);
    }

    [Fact]
    public void AnInactiveUserIsMappedAsInactive() => Assert.False(UserDto.FromDomain(Users.Inactive(), Clock.Now).IsActive);

    [Fact]
    public void TheLockBoundaryIsExclusive()
    {
        Assert.True(UserDto.FromDomain(Users.Locked(Clock.Now.AddTicks(1)), Clock.Now).IsLockedOut);
        Assert.False(UserDto.FromDomain(Users.Locked(Clock.Now), Clock.Now).IsLockedOut);
    }

    [Fact]
    public void ANullUserIsAnArgumentError() =>
        NullArguments.AssertThrowsArgumentNull(UserDto.FromDomain, null, Clock.Now);
}

public sealed class SignInOutcomeTests
{
    private static readonly SessionTokens Tokens = new("a", Clock.Now, "r", Clock.Now.AddDays(1));

    [Fact]
    public void SuccessCarriesTheTokensAndNoFailureCode()
    {
        SignInOutcome outcome = SignInOutcome.Success(Tokens);

        Assert.True(outcome.Succeeded);
        Assert.Equal(Tokens, outcome.Tokens);
        Assert.Null(outcome.FailureCode);
    }

    [Fact]
    public void FailedCarriesTheCodeAndNoTokens()
    {
        SignInOutcome outcome = SignInOutcome.Failed("Identity.InvalidCredentials");

        Assert.False(outcome.Succeeded);
        Assert.Null(outcome.Tokens);
        Assert.Equal("Identity.InvalidCredentials", outcome.FailureCode);
    }

    [Fact]
    public void TheRecordsCompareByValue()
    {
        Assert.Equal(new SessionTokens("a", Clock.Now, "r", Clock.Now), new SessionTokens("a", Clock.Now, "r", Clock.Now));
        Assert.Equal(new MeMembershipDto(Ids.DeptA1.Value, Ids.BranchA1.Value, "Agent"), new MeMembershipDto(Ids.DeptA1.Value, Ids.BranchA1.Value, "Agent"));
        Assert.Equal(
            new PortalAccountDto(Ids.Account, Ids.Customer, Ids.Contact, "a@b.com", null),
            new PortalAccountDto(Ids.Account, Ids.Customer, Ids.Contact, "a@b.com", null));
        Assert.NotEqual(
            new PortalAccountDto(Ids.Account, Ids.Customer, Ids.Contact, "a@b.com", null),
            new PortalAccountDto(Ids.Account, Ids.Customer, Ids.Contact, "c@b.com", null));
    }

    [Fact]
    public void MeDtoAndPortalMeDtoCompareByTheirMembers()
    {
        MeMembershipDto[] none = [];
        string[] perms = ["x"];

        var one = new MeDto(Ids.User, "e", "n", "en", Ids.TenantA.Value, null, none, perms, perms);

        Assert.Equal(one, new MeDto(Ids.User, "e", "n", "en", Ids.TenantA.Value, null, none, perms, perms));
        Assert.Equal(
            new PortalMeDto(Ids.Account, "e", "n", "en", Ids.Customer, Ids.Contact, true, perms),
            new PortalMeDto(Ids.Account, "e", "n", "en", Ids.Customer, Ids.Contact, true, perms));
    }
}

public sealed class IdentityModuleApiTests
{
    private readonly ISender _sender = Substitute.For<ISender>();

    [Fact]
    public async Task SendsExactlyTheGetStaffUsersQueryWithTheIdsCopiedToAList()
    {
        List<Guid> source = [Ids.User, Ids.OtherUser];
        GetStaffUsersQuery? sent = null;
        _sender.Send(Arg.Any<GetStaffUsersQuery>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                sent = call.Arg<GetStaffUsersQuery>();
                return Task.FromResult(Result.Success<IReadOnlyList<StaffUserSummary>>([]));
            });

        await new IdentityModuleApi(_sender).GetStaffUsersAsync(source, Stubs.Ct);
        source.Add(Guid.NewGuid());

        Assert.NotNull(sent);
        Assert.Equal([Ids.User, Ids.OtherUser], sent.UserIds);
    }

    [Fact]
    public async Task ReturnsTheSendersSuccessUnchanged()
    {
        StaffUserSummary[] summaries = [new(Ids.User, "Omar", true)];
        Result<IReadOnlyList<StaffUserSummary>> expected = Result.Success<IReadOnlyList<StaffUserSummary>>(summaries);
        _sender.Send(Arg.Any<GetStaffUsersQuery>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(expected));

        Result<IReadOnlyList<StaffUserSummary>> result = await new IdentityModuleApi(_sender).GetStaffUsersAsync([Ids.User], Stubs.Ct);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task ReturnsTheSendersFailureUnchanged()
    {
        Result<IReadOnlyList<StaffUserSummary>> expected = Result.Failure<IReadOnlyList<StaffUserSummary>>(Error.Forbidden("x.y", "no"));
        _sender.Send(Arg.Any<GetStaffUsersQuery>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(expected));

        Result<IReadOnlyList<StaffUserSummary>> result = await new IdentityModuleApi(_sender).GetStaffUsersAsync([Ids.User], Stubs.Ct);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task ForwardsTheCancellationToken()
    {
        using var source = new CancellationTokenSource();
        _sender.Send(Arg.Any<GetStaffUsersQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success<IReadOnlyList<StaffUserSummary>>([])));

        await new IdentityModuleApi(_sender).GetStaffUsersAsync([Ids.User], source.Token);

        await _sender.Received(1).Send(Arg.Any<GetStaffUsersQuery>(), source.Token);
    }
}
