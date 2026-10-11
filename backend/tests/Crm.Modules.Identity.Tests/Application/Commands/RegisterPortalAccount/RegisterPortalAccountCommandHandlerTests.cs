using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Customers.Contracts;
using Crm.Modules.Identity.Application;
using Crm.Modules.Identity.Application.Abstractions;
using Crm.Modules.Identity.Application.Commands.RegisterPortalAccount;
using Crm.Modules.Identity.Domain;
using Crm.Modules.Identity.Tests.Support;
using Crm.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Crm.Modules.Identity.Tests.Application.Commands.RegisterPortalAccount;

public sealed class RegisterPortalAccountCommandHandlerTests
{
    private const string Password = "Portal-pass-123";

    private readonly IRepository<PortalAccount, PortalAccountId> _accounts = Substitute.For<IRepository<PortalAccount, PortalAccountId>>();
    private readonly ICustomersModuleApi _customers = Substitute.For<ICustomersModuleApi>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IIdentityUnitOfWork _unitOfWork = Substitute.For<IIdentityUnitOfWork>();
    private readonly RecordingLogger<RegisterPortalAccountCommandHandler> _logger = new();
    private readonly List<PortalAccount> _added = [];

    public RegisterPortalAccountCommandHandlerTests()
    {
        _hasher.Hash(Password).Returns("hash-portal");
        _accounts.When(r => r.Add(Arg.Any<PortalAccount>())).Do(call => _added.Add(call.Arg<PortalAccount>()));
        _accounts.Lists();
        MatchesAre(CustomerFixtures.Match());
    }

    private void MatchesAre(params PortalContactMatch[] matches) =>
        _customers.FindPortalContactsByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success<IReadOnlyList<PortalContactMatch>>(matches)));

    private Task<Result> Run(string email = "customer@example.com", string? locale = null) =>
        new RegisterPortalAccountCommandHandler(_accounts, _customers, _hasher, _unitOfWork, _logger)
            .Handle(new RegisterPortalAccountCommand(email, Password, locale), Stubs.Ct);

    private async Task AssertSilentSuccess(Task<Result> run)
    {
        Result result = await run;

        Assert.True(result.IsSuccess);
        Assert.Empty(_added);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ThePasswordIsHashedOnEveryPathIncludingMalformedEmail()
    {
        await Run("not-an-email");
        await Run();
        _accounts.Lists(Accounts.New());
        await Run();

        _hasher.Received(3).Hash(Password);
    }

    [Fact]
    public async Task ThePasswordIsHashedBeforeAnyLookup()
    {
        var order = new List<string>();
        _hasher.Hash(Password).Returns(_ =>
        {
            order.Add("hash");
            return "hash-portal";
        });
        _accounts.ListAsync(Arg.Any<Specification<PortalAccount>>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                order.Add("list");
                return Task.FromResult<IReadOnlyList<PortalAccount>>([]);
            });

        await Run();

        Assert.Equal("hash", order[0]);
        Assert.Contains("list", order);
    }

    [Fact]
    public async Task AMalformedEmailIsASilentSuccessWithNoLookup()
    {
        await AssertSilentSuccess(Run("not-an-email"));

        await _accounts.DidNotReceive().ListAsync(Arg.Any<Specification<PortalAccount>>(), Arg.Any<CancellationToken>());
        await _customers.DidNotReceive().FindPortalContactsByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.True(_logger.Contains(LogLevel.Information, "malformed"));
    }

    [Fact]
    public async Task AnEmailAlreadyUsedByAPortalAccountIsASilentSuccessWithNoCustomersCall()
    {
        _accounts.Lists(Accounts.New());

        await AssertSilentSuccess(Run());

        await _customers.DidNotReceive().FindPortalContactsByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.True(_logger.Contains(LogLevel.Information, "taken"));
    }

    [Fact]
    public async Task ACustomersLookupFailureIsReturnedTheOneNonSilentPath()
    {
        _customers.FindPortalContactsByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Failure<IReadOnlyList<PortalContactMatch>>(Error.Failure("Customers.Boom", "boom"))));

        Result result = await Run();

        Expect.Failure(result, "Customers.Boom", ErrorType.Failure);
        Assert.Empty(_added);
    }

    [Fact]
    public async Task ZeroMatchesIsASilentSuccess()
    {
        MatchesAre();

        await AssertSilentSuccess(Run());

        Assert.True(_logger.Contains(LogLevel.Information, "unknown"));
    }

    [Fact]
    public async Task SeveralMatchesIsASilentSuccess()
    {
        MatchesAre(CustomerFixtures.Match(), CustomerFixtures.Match(contactId: Ids.OtherContact));

        await AssertSilentSuccess(Run());

        Assert.True(_logger.Contains(LogLevel.Information, "ambiguous"));
    }

    [Fact]
    public async Task AContactThatAlreadyHasAnAccountIsASilentSuccess()
    {
        _accounts.ListAsync(Arg.Any<Specification<PortalAccount>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                Specification<PortalAccount> spec = call.Arg<Specification<PortalAccount>>();
                IReadOnlyList<PortalAccount> items = Stubs.Matching(spec, Accounts.New(email: "someone-else@example.com"));
                return Task.FromResult(items);
            });

        await AssertSilentSuccess(Run());

        Assert.True(_logger.Contains(LogLevel.Information, "contact-taken"));
    }

    [Fact]
    public async Task ExactlyOneMatchCreatesTheAccountFromTheMatchNotTheRequest()
    {
        MatchesAre(CustomerFixtures.Match(customerId: Ids.OtherCustomer, contactId: Ids.OtherContact, name: " Match Name ", language: "ar", tenant: Ids.TenantB));

        Result result = await Run(" Customer@Example.COM ");

        Assert.True(result.IsSuccess);
        PortalAccount account = Assert.Single(_added);
        Assert.Equal(Ids.TenantB, account.TenantId);
        Assert.Equal(Ids.OtherCustomer, account.CustomerId);
        Assert.Equal(Ids.OtherContact, account.ContactId);
        Assert.Equal("customer@example.com", account.Email.Value);
        Assert.Equal("Match Name", account.DisplayName.Value);
        Assert.Same(PreferredLocale.Arabic, account.PreferredLocale);
        Assert.Equal("hash-portal", account.PasswordHash);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheRequestLocaleWinsOverTheMatchLanguage()
    {
        MatchesAre(CustomerFixtures.Match(language: "ar"));

        await Run(locale: "en");

        Assert.Same(PreferredLocale.English, Assert.Single(_added).PreferredLocale);
    }

    [Fact]
    public async Task WithNoRequestLocaleTheMatchLanguageIsUsed()
    {
        MatchesAre(CustomerFixtures.Match(language: "ar"));

        await Run(locale: null);

        Assert.Same(PreferredLocale.Arabic, Assert.Single(_added).PreferredLocale);
    }

    [Fact]
    public async Task TheCustomersLookupReceivesTheNormalisedEmail()
    {
        await Run(" Customer@Example.COM ");

        await _customers.Received(1).FindPortalContactsByEmailAsync("customer@example.com", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ARegistrationLogsIdsOnly()
    {
        await Run();

        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("registered", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NoLogLineContainsTheEmailOrThePassword()
    {
        await Run("not-an-email");
        MatchesAre();
        await Run();
        MatchesAre(CustomerFixtures.Match());
        await Run();
        _accounts.Lists(Accounts.New());
        await Run();

        Assert.NotEmpty(_logger.Entries);
        Assert.All(_logger.Entries, e =>
        {
            Assert.DoesNotContain("customer@example.com", e.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("not-an-email", e.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Password, e.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task AnUnsupportedMatchLanguageWithNoRequestLocaleIsADomainException()
    {
        MatchesAre(CustomerFixtures.Match(language: "fr"));

        await Expect.DomainCode(() => Run(), "Identity.LocaleUnsupported");
    }
}
