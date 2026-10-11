using Crm.Modules.Customers.Application;
using Crm.Modules.Customers.Application.Queries.FindContactsByEmail;
using Crm.Modules.Customers.Application.Queries.FindPortalContactsByEmail;
using Crm.Modules.Customers.Application.Queries.GetCustomerById;
using Crm.Modules.Customers.Application.Queries.GetCustomerNames;
using Crm.Modules.Customers.Application.Queries.GetPortalContact;
using Crm.Modules.Customers.Contracts;
using Crm.Modules.Customers.Domain;
using Crm.Modules.Customers.Tests.Support;
using Crm.SharedKernel.Results;
using MediatR;

namespace Crm.Modules.Customers.Tests.Application;

public sealed class CustomersModuleApiTests
{
    private readonly ISender _sender = Substitute.For<ISender>();

    private CustomersModuleApi Api() => new(_sender);

    /// <summary>Every request the api sends, in order, with the token it was sent with.</summary>
    private readonly List<object> _requests = [];

    private readonly List<CancellationToken> _tokens = [];

    private void Answers<T>(Result<T> answer) =>
        _sender.Send(Arg.Any<IRequest<Result<T>>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            _requests.Add(call.Arg<IRequest<Result<T>>>());
            _tokens.Add(call.Arg<CancellationToken>());
            return Task.FromResult(answer);
        });

    private static CustomerDto Dto(string status = "Active")
    {
        Customer customer = CustomerFixtures.Organisation(3, "نجد", "Najd", "Najd Co", "ar", id: Ids.Customer1);
        customer.AddContact("Sara", null, CustomerFixtures.Email("sara@example.com"), CustomerFixtures.Phone("+966500000001"), true, true, Ids.Contact1);
        customer.AddContact("Omar", null, null, CustomerFixtures.Phone("+966500000002"), false, false, Ids.Contact2);
        if (status == "Inactive")
        {
            customer.Deactivate();
        }

        return CustomerDto.FromDomain(customer);
    }

    [Fact]
    public async Task TheSummaryIsMappedFromTheCustomerDtoThroughTheGetByIdQuery()
    {
        Answers(Result.Success(Dto()));

        Result<CustomerSummary> result = await Api().GetCustomerSummaryAsync(Ids.Customer1.Value, Stubs.Ct);

        Assert.Equal(new GetCustomerByIdQuery(Ids.Customer1.Value), Assert.Single(_requests));
        CustomerSummary summary = result.Value;
        Assert.Equal(Ids.Customer1.Value, summary.Id);
        Assert.Equal("C-000003", summary.Number);
        Assert.Equal("نجد", summary.DisplayNameAr);
        Assert.Equal("Najd", summary.DisplayNameEn);
        Assert.Equal("Organisation", summary.Type);
        Assert.True(summary.IsActive);
        Assert.Equal("ar", summary.PreferredLanguage);
        Assert.Equal(Ids.TenantA.Value, summary.TenantId);
        Assert.Equal(Ids.BranchA1.Value, summary.BranchId);
        Assert.Equal(Ids.DepartmentA1.Value, summary.DepartmentId);
        Assert.Equal(
            [new CustomerContactSummary(Ids.Contact1, "Sara", "sara@example.com", "+966500000001", true, true),
             new CustomerContactSummary(Ids.Contact2, "Omar", null, "+966500000002", false, false)],
            summary.Contacts);
    }

    [Fact]
    public async Task AnInactiveCustomerIsNotActiveInTheSummary()
    {
        Answers(Result.Success(Dto("Inactive")));

        Result<CustomerSummary> result = await Api().GetCustomerSummaryAsync(Ids.Customer1.Value, Stubs.Ct);

        Assert.False(result.Value.IsActive);
    }

    [Theory]
    [InlineData("Customers.CustomerNotFound", ErrorType.NotFound)]
    [InlineData("Organisation.OutOfScope", ErrorType.Forbidden)]
    public async Task AFailureIsPassedThroughWithItsCodeAndType(string code, ErrorType type)
    {
        Error error = type == ErrorType.NotFound ? Error.NotFound(code, "missing") : Error.Forbidden(code, "denied");
        Answers(Result.Failure<CustomerDto>(error));

        Result<CustomerSummary> result = await Api().GetCustomerSummaryAsync(Ids.Customer1.Value, Stubs.Ct);

        Expect.Failure(result, code, type);
    }

    [Fact]
    public async Task TheNamesQueryGetsACopyOfTheIdsAndTheSendersResultUnchanged()
    {
        IReadOnlyList<CustomerNameSummary> names = [new CustomerNameSummary(Ids.Customer1.Value, "C-000001", "أ", "A")];
        Answers(Result.Success(names));
        List<Guid> ids = [Ids.Customer1.Value, Ids.Customer2.Value];

        Result<IReadOnlyList<CustomerNameSummary>> result = await Api().GetCustomerNamesAsync(ids, Stubs.Ct);
        ids.Add(Ids.Customer3.Value);

        GetCustomerNamesQuery sent = Assert.IsType<GetCustomerNamesQuery>(Assert.Single(_requests));
        Assert.Equal([Ids.Customer1.Value, Ids.Customer2.Value], sent.CustomerIds);
        Assert.Same(names, result.Value);
    }

    [Fact]
    public async Task TheEmailLookupsSendTheMatchingQueryWithTheExactArgumentAndPassTheResultThrough()
    {
        IReadOnlyList<CustomerContactMatch> staff = [];
        IReadOnlyList<PortalContactMatch> portal = [];
        Answers(Result.Success(staff));
        Answers(Result.Success(portal));

        Result<IReadOnlyList<CustomerContactMatch>> staffResult = await Api().FindContactByEmailAsync(" A@B.co ", Stubs.Ct);
        Result<IReadOnlyList<PortalContactMatch>> portalResult = await Api().FindPortalContactsByEmailAsync(" A@B.co ", Stubs.Ct);

        Assert.Equal(new FindContactsByEmailQuery(" A@B.co "), _requests[0]);
        Assert.Equal(new FindPortalContactsByEmailQuery(" A@B.co "), _requests[1]);
        Assert.Same(staff, staffResult.Value);
        Assert.Same(portal, portalResult.Value);
    }

    [Fact]
    public async Task ThePortalContactLookupSendsTheContactIdAndPassesTheResultThrough()
    {
        var match = new PortalContactMatch(Ids.Customer1.Value, Ids.Contact1, "Sara", "sara@example.com", true, "ar", Ids.TenantA.Value, Ids.BranchA1.Value, Ids.DepartmentA1.Value);
        Answers(Result.Success(match));

        Result<PortalContactMatch> result = await Api().GetPortalContactAsync(Ids.Contact1, Stubs.Ct);

        Assert.Equal(new GetPortalContactQuery(Ids.Contact1), Assert.Single(_requests));
        Assert.Same(match, result.Value);
    }

    [Fact]
    public async Task APortalContactFailureIsPassedThrough()
    {
        Answers(Result.Failure<PortalContactMatch>(Error.NotFound("Customers.PortalContactNotFound", "none")));

        Result<PortalContactMatch> result = await Api().GetPortalContactAsync(Ids.Contact1, Stubs.Ct);

        Expect.Failure(result, "Customers.PortalContactNotFound", ErrorType.NotFound);
    }

    [Fact]
    public async Task TheCancellationTokenIsForwardedOnAllFiveCalls()
    {
        Answers(Result.Success(Dto()));
        Answers(Result.Success<IReadOnlyList<CustomerNameSummary>>([]));
        Answers(Result.Success<IReadOnlyList<CustomerContactMatch>>([]));
        Answers(Result.Success<IReadOnlyList<PortalContactMatch>>([]));
        Answers(Result.Success(new PortalContactMatch(Ids.Customer1.Value, Ids.Contact1, "S", "s@b.co", true, "en", Ids.TenantA.Value, Ids.BranchA1.Value, Ids.DepartmentA1.Value)));
        using var source = new CancellationTokenSource();
        CancellationToken token = source.Token;

        await Api().GetCustomerSummaryAsync(Ids.Customer1.Value, token);
        await Api().GetCustomerNamesAsync([Ids.Customer1.Value], token);
        await Api().FindContactByEmailAsync("a@b.co", token);
        await Api().FindPortalContactsByEmailAsync("a@b.co", token);
        await Api().GetPortalContactAsync(Ids.Contact1, token);

        Assert.Equal(5, _tokens.Count);
        Assert.All(_tokens, t => Assert.Equal(token, t));
    }
}
