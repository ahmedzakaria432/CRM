using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Paging;
using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Identity.Application;
using Crm.Modules.Identity.Application.Queries.GetStaffUsers;
using Crm.Modules.Identity.Application.Queries.GetUser;
using Crm.Modules.Identity.Application.Queries.ListUsers;
using Crm.Modules.Identity.Contracts;
using Crm.Modules.Identity.Domain;
using Crm.Modules.Identity.Infrastructure.Persistence;
using Crm.Modules.Identity.Tests.Support;
using Crm.SharedKernel.Results;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Identity.Tests.Application.Queries;

public sealed class GetUserQueryHandlerTests
{
    private readonly IRepository<User, UserId> _users = Substitute.For<IRepository<User, UserId>>();

    private Task<Result<UserDto>> Run() =>
        new GetUserQueryHandler(_users, CurrentUsers.Admin(), Clock.Fixed()).Handle(new GetUserQuery(Ids.User), Stubs.Ct);

    [Fact]
    public async Task AMissingUserIsNotFound() => Expect.Failure(await Run(), "Identity.UserNotFound", ErrorType.NotFound);

    [Fact]
    public async Task AUserOfAnotherTenantIsForbidden()
    {
        _users.Holds(Users.New(Ids.TenantB));

        Expect.Failure(await Run(), "Organisation.OutOfScope", ErrorType.Forbidden);
    }

    [Fact]
    public async Task SuccessMapsTheDto()
    {
        User user = Users.New(locale: "ar");
        user.RecordSuccessfulSignIn(Clock.Now.AddDays(-1));
        _users.Holds(user);

        UserDto dto = (await Run()).Value;

        Assert.Equal(Ids.User, dto.Id);
        Assert.Equal("agent@example.com", dto.Email);
        Assert.Equal("Omar Haddad", dto.DisplayName);
        Assert.Equal("ar", dto.PreferredLocale);
        Assert.True(dto.IsActive);
        Assert.False(dto.IsLockedOut);
        Assert.Equal(Clock.Now.AddDays(-1), dto.LastSignInAtUtc);
    }

    [Fact]
    public async Task IsLockedOutIsComputedAtTheClockTime()
    {
        _users.Holds(Users.Locked(Clock.Now.AddTicks(1)));
        Assert.True((await Run()).Value.IsLockedOut);

        _users.Holds(Users.Locked(Clock.Now));
        Assert.False((await Run()).Value.IsLockedOut);
    }
}

public sealed class ListUsersQueryHandlerTests
{
    private readonly string _database = IdentityTestContext.NewDatabaseName();

    private async Task SeedAsync()
    {
        await using IdentityDbContext store = IdentityTestContext.Store(_database);
        store.Users.AddRange(
            Users.New(email: "amal@example.com", name: "Amal", id: UserId.New()),
            Users.New(email: "bilal@example.com", name: "Bilal", id: UserId.New()),
            Users.New(email: "carim@sales.example.com", name: "Carim", id: UserId.New()),
            Users.New(email: "dina@example.com", name: "Dina", id: UserId.New()),
            Users.New(email: "esam@example.com", name: "Esam", id: UserId.New()),
            Users.New(Ids.TenantB, "other@example.com", "Aaron Other", id: UserId.New()));
        await store.SaveChangesAsync(Stubs.Ct);
    }

    private async Task<PagedResult<UserDto>> Run(string? search = null, int page = 1, int pageSize = 20)
    {
        await using IdentityDbContext context = IdentityTestContext.Create(_database, CurrentUsers.Admin());
        var handler = new ListUsersQueryHandler(IdentityTestContext.Repo<User, UserId>(context), CurrentUsers.Admin(), Clock.Fixed());

        Result<PagedResult<UserDto>> result = await handler.Handle(new ListUsersQuery(search, page, pageSize), Stubs.Ct);

        return result.Value;
    }

    [Fact]
    public async Task TenantBUsersAreNeverReturnedAndTheTotalExcludesThem()
    {
        await SeedAsync();

        PagedResult<UserDto> result = await Run();

        Assert.Equal(5, result.Total);
        Assert.DoesNotContain(result.Items, u => u.Email == "other@example.com");
    }

    [Fact]
    public async Task UsersAreOrderedByDisplayName()
    {
        await SeedAsync();

        PagedResult<UserDto> result = await Run();

        Assert.Equal(["Amal", "Bilal", "Carim", "Dina", "Esam"], result.Items.Select(u => u.DisplayName));
    }

    [Fact]
    public async Task SearchMatchesAnEmailFragment()
    {
        await SeedAsync();

        PagedResult<UserDto> result = await Run("sales");

        Assert.Equal("Carim", Assert.Single(result.Items).DisplayName);
        Assert.Equal(1, result.Total);
    }

    [Fact]
    public async Task SearchMatchesADisplayNameFragment()
    {
        await SeedAsync();

        PagedResult<UserDto> result = await Run("ilal");

        Assert.Equal("Bilal", Assert.Single(result.Items).DisplayName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ABlankSearchIsNoFilter(string? search)
    {
        await SeedAsync();

        Assert.Equal(5, (await Run(search)).Total);
    }

    [Fact]
    public async Task PagingReturnsTheRequestedPage()
    {
        await SeedAsync();

        PagedResult<UserDto> result = await Run(page: 2, pageSize: 2);

        Assert.Equal(["Carim", "Dina"], result.Items.Select(u => u.DisplayName));
        Assert.Equal(5, result.Total);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
    }

    [Fact]
    public async Task TheLastPageIsPartial()
    {
        await SeedAsync();

        PagedResult<UserDto> result = await Run(page: 3, pageSize: 2);

        Assert.Equal("Esam", Assert.Single(result.Items).DisplayName);
    }

    [Fact]
    public async Task APageBeyondTheEndIsEmptyWithTheRightTotal()
    {
        await SeedAsync();

        PagedResult<UserDto> result = await Run(page: 9, pageSize: 2);

        Assert.Empty(result.Items);
        Assert.Equal(5, result.Total);
    }

    [Fact]
    public async Task TheTotalIgnoresPaging()
    {
        await SeedAsync();

        Assert.Equal(5, (await Run(page: 1, pageSize: 1)).Total);
    }

    [Fact]
    public async Task IsLockedOutIsComputedPerRow()
    {
        await using (IdentityDbContext store = IdentityTestContext.Store(_database))
        {
            store.Users.AddRange(
                Users.Locked(Clock.Now.AddMinutes(5)),
                Users.New(email: "free@example.com", name: "Free", id: UserId.New()));
            await store.SaveChangesAsync(Stubs.Ct);
        }

        PagedResult<UserDto> result = await Run();

        Assert.Equal(1, result.Items.Count(u => u.IsLockedOut));
    }

    [Fact]
    public async Task TheHandlerAsksForAPagedAndAnUnpagedSpecification()
    {
        var users = Substitute.For<IRepository<User, UserId>>();
        users.Lists();
        users.CountsAs(0);

        await new ListUsersQueryHandler(users, CurrentUsers.Admin(), Clock.Fixed()).Handle(new ListUsersQuery("x", 2, 3), Stubs.Ct);

        await users.Received(1).ListAsync(Arg.Is<Specification<User>>(s => s.Skip == 3 && s.Take == 3), Arg.Any<CancellationToken>());
        await users.Received(1).CountAsync(Arg.Is<Specification<User>>(s => s.Skip == null && s.Take == null), Arg.Any<CancellationToken>());
    }
}

public sealed class GetStaffUsersQueryHandlerTests
{
    private readonly string _database = IdentityTestContext.NewDatabaseName();

    private static readonly Guid FirstId = new("11111111-0000-0000-0000-000000000001");
    private static readonly Guid SecondId = new("11111111-0000-0000-0000-000000000002");
    private static readonly Guid ForeignId = new("11111111-0000-0000-0000-000000000003");

    private async Task SeedAsync()
    {
        await using IdentityDbContext store = IdentityTestContext.Store(_database);
        store.Users.AddRange(
            Users.New(email: "a@example.com", name: "Ayman", id: new UserId(FirstId)),
            Users.New(email: "b@example.com", name: "Basma", id: new UserId(SecondId)),
            Users.New(Ids.TenantB, "c@example.com", "Foreign", id: new UserId(ForeignId)));
        User inactive = Users.New(email: "d@example.com", name: "Dormant", id: new UserId(Ids.OtherUser));
        inactive.Deactivate(Clock.Now);
        store.Users.Add(inactive);
        await store.SaveChangesAsync(Stubs.Ct);
    }

    private async Task<IReadOnlyList<StaffUserSummary>> Run(params Guid[] ids)
    {
        await using IdentityDbContext context = IdentityTestContext.Create(_database, CurrentUsers.Admin());
        var handler = new GetStaffUsersQueryHandler(IdentityTestContext.Repo<User, UserId>(context), CurrentUsers.Admin());

        Result<IReadOnlyList<StaffUserSummary>> result = await handler.Handle(new GetStaffUsersQuery(ids), Stubs.Ct);

        return result.Value;
    }

    [Fact]
    public async Task EmptyIdsReturnAnEmptySuccessWithoutTouchingTheRepository()
    {
        var users = Substitute.For<IRepository<User, UserId>>();

        Result<IReadOnlyList<StaffUserSummary>> result = await new GetStaffUsersQueryHandler(users, CurrentUsers.Admin())
            .Handle(new GetStaffUsersQuery([]), Stubs.Ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        await users.DidNotReceive().ListAsync(Arg.Any<Specification<User>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnlyUsersOfTheCallersTenantAreReturned()
    {
        await SeedAsync();

        IReadOnlyList<StaffUserSummary> result = await Run(FirstId, ForeignId);

        Assert.Equal(FirstId, Assert.Single(result).Id);
    }

    [Fact]
    public async Task UnknownIdsAreAbsentNotAnError()
    {
        await SeedAsync();

        IReadOnlyList<StaffUserSummary> result = await Run(FirstId, Guid.NewGuid());

        Assert.Equal(FirstId, Assert.Single(result).Id);
    }

    [Fact]
    public async Task DuplicateIdsAreCollapsed()
    {
        await SeedAsync();

        IReadOnlyList<StaffUserSummary> result = await Run(FirstId, FirstId, FirstId);

        Assert.Single(result);
    }

    [Fact]
    public async Task TheSummaryCarriesIdNameAndActiveFlagIncludingInactiveUsers()
    {
        await SeedAsync();

        IReadOnlyList<StaffUserSummary> result = await Run(FirstId, Ids.OtherUser);

        Assert.Contains(new StaffUserSummary(FirstId, "Ayman", true), result);
        Assert.Contains(new StaffUserSummary(Ids.OtherUser, "Dormant", false), result);
    }

    [Fact]
    public async Task TheResultIsReadOnly()
    {
        await SeedAsync();

        IReadOnlyList<StaffUserSummary> result = await Run(FirstId);

        Assert.False(result is List<StaffUserSummary>);
        Assert.Throws<NotSupportedException>(() => ((IList<StaffUserSummary>)result).Clear());
    }
}

public sealed class GetStaffUsersQueryValidatorTests
{
    private static readonly GetStaffUsersQueryValidator Validator = new();

    private static GetStaffUsersQuery Of(int count) => new([.. Enumerable.Range(0, count).Select(_ => Guid.NewGuid())]);

    [Fact]
    public void EmptyIsValid() => Expect.Valid(Validator.Validate(Of(0)));

    [Fact]
    public void TwoHundredIdsAreValid() => Expect.Valid(Validator.Validate(Of(200)));

    [Fact]
    public void TwoHundredAndOneIdsAreInvalid() => Expect.Invalid(Validator.Validate(Of(201)), "UserIds");

    [Fact]
    public void ANullListMakesTheValidatorThrowRatherThanFail()
    {
        GetStaffUsersQuery query = Assert.IsType<GetStaffUsersQuery>(Activator.CreateInstance(typeof(GetStaffUsersQuery), new object?[] { null }));

        Assert.Throws<NullReferenceException>(() => Validator.Validate(query));
    }

}

public sealed class ListUsersQueryValidatorTests
{
    private static readonly ListUsersQueryValidator Validator = new();

    [Fact]
    public void TheDefaultsAreFirstPageOfTwenty()
    {
        var query = new ListUsersQuery(null);

        Assert.Equal(1, query.Page);
        Assert.Equal(20, query.PageSize);
        Expect.Valid(Validator.Validate(query));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AnInvalidPageFails(int page) => Expect.Invalid(Validator.Validate(new ListUsersQuery(null, page)), "Page");

    [Fact]
    public void PageOneIsValid() => Expect.Valid(Validator.Validate(new ListUsersQuery(null, 1)));

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void AnInvalidPageSizeFails(int size) => Expect.Invalid(Validator.Validate(new ListUsersQuery(null, 1, size)), "PageSize");

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void ABoundaryPageSizeIsValid(int size) => Expect.Valid(Validator.Validate(new ListUsersQuery(null, 1, size)));

    [Fact]
    public void ASearchOfTwoHundredIsValidAndTwoHundredAndOneIsNot()
    {
        Expect.Valid(Validator.Validate(new ListUsersQuery(new string('s', 200))));
        Expect.Invalid(Validator.Validate(new ListUsersQuery(new string('s', 201))), "Search");
    }

    [Fact]
    public void ANullSearchIsValid() => Expect.Valid(Validator.Validate(new ListUsersQuery(null)));
}
