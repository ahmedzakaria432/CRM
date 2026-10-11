using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Specifications;
using Crm.Modules.Customers.Application;
using Crm.Modules.Customers.Application.Abstractions;
using Crm.Modules.Customers.Application.Commands.CreateCustomer;
using Crm.Modules.Customers.Application.Specifications;
using Crm.Modules.Customers.Contracts.IntegrationEvents;
using Crm.Modules.Customers.Domain;
using Crm.Modules.Customers.Tests.Support;
using Crm.SharedKernel.Results;

namespace Crm.Modules.Customers.Tests.Application.Commands.CreateCustomer;

public sealed class CreateCustomerCommandHandlerTests
{
    private readonly Harness _harness = new();
    private readonly ICustomerNumberGenerator _numbers = Stubs.Numbers(7);

    private CreateCustomerCommandHandler Handler(OrganisationScope? scope = null) =>
        new(_harness.Repository, _harness.UnitOfWork, CurrentUsers.Agent(scope ?? Scopes.AgentA1()), _numbers, _harness.Clock, _harness.Bus);

    private static CreateCustomerCommand Command(
        string? ar = "أحمد", string? en = "Ahmed", string type = "Individual", string? legalName = null,
        string language = "en", IReadOnlyList<NewContact>? contacts = null, bool confirm = false, Guid? department = null) =>
        new(ar, en, type, legalName, language, contacts ?? [], confirm, department);

    private static NewContact Contact(
        string name = "Sara", string? email = "sara@example.com", string? phone = null, bool primary = false, bool portal = false) =>
        new(name, null, email, phone, primary, portal);

    private Task<Result<CreateCustomerOutcome>> Send(CreateCustomerCommand command, OrganisationScope? scope = null) =>
        Handler(scope).Handle(command, Stubs.Ct);

    // ---- scope ----

    [Fact]
    public async Task ARequestedDepartmentOutsideTheScopeIsForbiddenAndNothingHappens()
    {
        Result<CreateCustomerOutcome> result = await Send(Command(department: Ids.DepartmentB1.Value));

        Expect.Failure(result, "Organisation.OutOfScope", ErrorType.Forbidden);
        await _numbers.DidNotReceive().NextAsync(Arg.Any<CancellationToken>());
        Assert.Empty(_harness.Log);
    }

    [Fact]
    public async Task AnAmbiguousScopeWithoutADepartmentAsksForOne()
    {
        Result<CreateCustomerOutcome> result = await Send(Command(), Scopes.AgentA1AndA2());

        Expect.Failure(result, "Organisation.DepartmentRequired", ErrorType.Validation);
        Assert.StartsWith("DepartmentId:", result.Error.Description, StringComparison.Ordinal);
        await _numbers.DidNotReceive().NextAsync(Arg.Any<CancellationToken>());
        Assert.Empty(_harness.Log);
    }

    [Fact]
    public async Task ASingleDepartmentResolvesWithoutARequest()
    {
        Result<CreateCustomerOutcome> result = await Send(Command());

        CustomerDto dto = Assert.IsType<CustomerDto>(result.Value.Customer);
        Assert.Equal(Ids.DepartmentA1.Value, dto.DepartmentId);
        Assert.Equal(Ids.BranchA1.Value, dto.BranchId);
        Assert.Equal(Ids.TenantA.Value, dto.TenantId);
    }

    [Fact]
    public async Task AnActiveDepartmentResolvesWithoutARequest()
    {
        OrganisationScope scope = Scopes.Narrowed(
            Ids.TenantA, [Scopes.Member(Ids.DepartmentA1, Ids.BranchA1), Scopes.Member(Ids.DepartmentA2, Ids.BranchA2)], Ids.DepartmentA2);

        Result<CreateCustomerOutcome> result = await Send(Command(), scope);

        Assert.Equal(Ids.DepartmentA2.Value, result.Value.Customer?.DepartmentId);
        Assert.Equal(Ids.BranchA2.Value, result.Value.Customer?.BranchId);
    }

    [Fact]
    public async Task ARequestedDepartmentInScopeBeatsTheActiveOne()
    {
        OrganisationScope scope = Scopes.Agent(
            Ids.TenantA, Scopes.Member(Ids.DepartmentA1, Ids.BranchA1), Scopes.Member(Ids.DepartmentA2, Ids.BranchA2));

        Result<CreateCustomerOutcome> result = await Send(Command(department: Ids.DepartmentA2.Value), scope);

        Assert.Equal(Ids.DepartmentA2.Value, result.Value.Customer?.DepartmentId);
        Assert.Equal(Ids.BranchA2.Value, result.Value.Customer?.BranchId);
    }

    // ---- success ----

    [Fact]
    public async Task ACustomerIsAddedSavedAndReturnedWithTheGeneratedNumber()
    {
        Result<CreateCustomerOutcome> result = await Send(Command(
            type: "Organisation", legalName: "Najd Co", language: "ar", contacts: [Contact("Sara", "sara@example.com", "+966500000001")]));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Created);
        CustomerDto dto = Assert.IsType<CustomerDto>(result.Value.Customer);
        Assert.Equal("C-000007", dto.Number);
        Assert.Equal("Organisation", dto.Type);
        Assert.Equal("Najd Co", dto.LegalName);
        Assert.Equal("ar", dto.PreferredLanguage);
        Assert.Equal("Active", dto.Status);
        ContactDto contact = Assert.Single(dto.Contacts);
        Assert.True(contact.IsPrimary);
        Assert.Equal("+966500000001", contact.Phone);
        await _numbers.Received(1).NextAsync(Arg.Any<CancellationToken>());
        _harness.Repository.Received(1).Add(Arg.Is<Customer>(c => c.Number.Value == "C-000007"));
        Assert.Equal(["add", "save", "publish"], _harness.Log);
    }

    [Fact]
    public async Task AMissingNameSideIsFilledFromTheOther()
    {
        Result<CreateCustomerOutcome> result = await Send(Command(ar: null, en: "Ahmed"));

        Assert.Equal("Ahmed", result.Value.Customer?.DisplayName.Ar);
        Assert.Equal("Ahmed", result.Value.Customer?.DisplayName.En);
    }

    [Theory]
    [InlineData("individual", "Individual")]
    [InlineData("INDIVIDUAL", "Individual")]
    public async Task TheTypeIsParsedWithoutRegardToCase(string input, string expected)
    {
        Result<CreateCustomerOutcome> result = await Send(Command(type: input));

        Assert.Equal(expected, result.Value.Customer?.Type);
    }

    [Theory]
    [InlineData("organisation")]
    [InlineData("ORGANISATION")]
    public async Task AnOrganisationTypeInAnyCaseIsAccepted(string input)
    {
        Result<CreateCustomerOutcome> result = await Send(Command(type: input, legalName: "Najd Co"));

        Assert.Equal("Organisation", result.Value.Customer?.Type);
    }

    [Fact]
    public async Task ContactsAreAddedInOrderAndTheFirstIsPrimary()
    {
        Result<CreateCustomerOutcome> result = await Send(Command(contacts:
        [
            Contact("Alice", "a@example.com"),
            Contact("Bob", "b@example.com"),
        ]));

        CustomerDto dto = Assert.IsType<CustomerDto>(result.Value.Customer);
        Assert.Equal(["Alice", "Bob"], dto.Contacts.Select(c => c.Name));
        Assert.True(dto.Contacts[0].IsPrimary);
        Assert.False(dto.Contacts[1].IsPrimary);
    }

    [Fact]
    public async Task ALaterPrimaryContactDemotesTheEarlierOne()
    {
        Result<CreateCustomerOutcome> result = await Send(Command(contacts:
        [
            Contact("Alice", "a@example.com"),
            Contact("Bob", "b@example.com", primary: true),
        ]));

        CustomerDto dto = Assert.IsType<CustomerDto>(result.Value.Customer);
        Assert.Equal("Bob", dto.Contacts[0].Name);
        Assert.True(dto.Contacts[0].IsPrimary);
        Assert.False(dto.Contacts[1].IsPrimary);
    }

    [Fact]
    public async Task ABlankEmailOrPhoneCountsAsAbsent()
    {
        Result<CreateCustomerOutcome> result = await Send(Command(contacts: [Contact("Sara", "   ", "+966500000001")]));

        ContactDto contact = Assert.Single(result.Value.Customer?.Contacts ?? []);
        Assert.Null(contact.Email);
        Assert.Equal("+966500000001", contact.Phone);
    }

    [Fact]
    public async Task WithoutContactsNoDuplicateLookupIsMade()
    {
        await Send(Command());

        await _harness.Repository.DidNotReceive().ListAsync(Arg.Any<Specification<Customer>>(), Arg.Any<CancellationToken>());
    }

    // ---- integration event ----

    [Fact]
    public async Task TheCreatedEventIsPublishedOnceAfterTheSave()
    {
        await Send(Command(type: "Organisation", legalName: "Najd Co", ar: "نجد", en: "Najd"));

        CustomerCreatedIntegrationEvent published = Assert.IsType<CustomerCreatedIntegrationEvent>(Assert.Single(_harness.Published));
        Assert.NotEqual(Guid.Empty, published.Id);
        Assert.Equal(Clock.Now, published.OccurredOnUtc);
        Assert.NotEqual(Guid.Empty, published.CustomerId);
        Assert.Equal("C-000007", published.Number);
        Assert.Equal("نجد", published.DisplayNameAr);
        Assert.Equal("Najd", published.DisplayNameEn);
        Assert.Equal("Organisation", published.Type);
        Assert.Equal(Ids.TenantA.Value, published.TenantId);
        Assert.Equal(Ids.BranchA1.Value, published.BranchId);
        Assert.Equal(Ids.DepartmentA1.Value, published.DepartmentId);
        Assert.Equal("save", _harness.Log[^2]);
        Assert.Equal("publish", _harness.Log[^1]);
    }

    [Fact]
    public async Task EveryPublishedEventGetsAFreshId()
    {
        await Send(Command());
        await Send(Command());

        Assert.Equal(2, _harness.Published.Select(e => e.Id).Distinct().Count());
    }

    // ---- duplicate warning ----

    private Customer ExistingWith(string? email, string? phone, long sequence = 1)
    {
        Customer existing = CustomerFixtures.Individual(sequence, id: new CustomerId(Guid.NewGuid()));
        existing.AddSimpleContact("Other", email, phone);
        existing.AddSimpleContact("Third", $"third{sequence}@example.com", $"+9665100000{sequence:D2}");

        _harness.Repository.Lists(existing);
        return existing;
    }

    [Fact]
    public async Task AnEmailMatchReturnsTheCandidatesAndCreatesNothing()
    {
        Customer existing = ExistingWith("dup@example.com", null);

        Result<CreateCustomerOutcome> result = await Send(Command(contacts: [Contact("Sara", " DUP@Example.com ")]));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Created);
        Assert.Null(result.Value.Customer);
        DuplicateCandidateDto candidate = Assert.Single(result.Value.DuplicateCandidates);
        Assert.Equal(existing.Id.Value, candidate.Id);
        Assert.Equal("C-000001", candidate.Number);
        Assert.Equal("Active", candidate.Status);
        Assert.Equal(["dup@example.com"], candidate.MatchedEmails);
        Assert.Empty(candidate.MatchedPhones);
        await _numbers.DidNotReceive().NextAsync(Arg.Any<CancellationToken>());
        Assert.Empty(_harness.Log);
        Assert.Empty(_harness.Published);
    }

    [Fact]
    public async Task APhoneOnlyMatchIsReportedWithOnlyTheMatchingChannel()
    {
        ExistingWith("someone@example.com", "+966555000111");

        Result<CreateCustomerOutcome> result = await Send(Command(contacts: [Contact("Sara", null, "+966 555 000 111")]));

        DuplicateCandidateDto candidate = Assert.Single(result.Value.DuplicateCandidates);
        Assert.Equal(["+966555000111"], candidate.MatchedPhones);
        Assert.Empty(candidate.MatchedEmails);
    }

    [Fact]
    public async Task SeveralMatchesGiveOneCandidateEachInOrder()
    {
        Customer first = CustomerFixtures.Individual(1, id: new CustomerId(Guid.NewGuid()));
        first.AddSimpleContact("A", "dup@example.com");
        Customer second = CustomerFixtures.Individual(2, id: new CustomerId(Guid.NewGuid()));
        second.AddSimpleContact("B", "dup@example.com");
        _harness.Repository.Lists(second, first);

        Result<CreateCustomerOutcome> result = await Send(Command(contacts: [Contact("Sara", "dup@example.com")]));

        Assert.Equal([first.Id.Value, second.Id.Value], result.Value.DuplicateCandidates.Select(c => c.Id));
    }

    [Fact]
    public async Task ANonMatchingLookupCreatesTheCustomer()
    {
        ExistingWith("someone@example.com", null);

        Result<CreateCustomerOutcome> result = await Send(Command(contacts: [Contact("Sara", "new@example.com")]));

        Assert.True(result.Value.Created);
        await _harness.Repository.Received(1).ListAsync(Arg.Any<Specification<Customer>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ContactsWithoutChannelsDoNotTriggerALookup()
    {
        // Pinned: with no email and no phone the lookup is skipped, and the aggregate then refuses the contact.
        await Assert.ThrowsAsync<Crm.SharedKernel.DomainException>(() => Send(Command(contacts: [Contact("Sara", null, null)])));

        await _harness.Repository.DidNotReceive().ListAsync(Arg.Any<Specification<Customer>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmingTheDuplicateSkipsTheLookupAndCreatesTheCustomer()
    {
        ExistingWith("dup@example.com", null);

        Result<CreateCustomerOutcome> result = await Send(Command(contacts: [Contact("Sara", "dup@example.com")], confirm: true));

        Assert.True(result.Value.Created);
        await _harness.Repository.DidNotReceive().ListAsync(Arg.Any<Specification<Customer>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheLookupUsesTheChannelsSpecification()
    {
        ExistingWith("someone@example.com", null);

        await Send(Command(contacts: [Contact("Sara", "dup@example.com", "+966500000001")]));

        await _harness.Repository.Received(1).ListAsync(
            Arg.Is<Specification<Customer>>(s => s is CustomersWithContactChannelsSpecification), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheLookupSpecificationReceivesTheNormalisedDistinctChannels()
    {
        Specification<Customer>? seen = null;
        _harness.Repository.ListAsync(Arg.Do<Specification<Customer>>(s => seen = s), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Customer>>([]));
        Customer probeByEmail = CustomerFixtures.Individual(1);
        probeByEmail.AddSimpleContact("P", "dup@example.com");
        Customer probeByPhone = CustomerFixtures.Individual(2);
        probeByPhone.AddSimpleContact("P", null, "+966500000001");
        Customer probeOther = CustomerFixtures.Individual(3);
        probeOther.AddSimpleContact("P", "other@example.com", "+966500000002");

        // The request repeats the channels, so the aggregate refuses it after the lookup; only the lookup is under test.
        await Assert.ThrowsAsync<Crm.SharedKernel.DomainException>(() => Send(Command(contacts:
        [
            Contact("A", " DUP@Example.com ", "+966 50 000 0001"),
            Contact("B", "dup@example.com", "+966500000001"),
        ])));

        CustomersWithContactChannelsSpecification spec = Assert.IsType<CustomersWithContactChannelsSpecification>(seen);
        Assert.Equal([probeByEmail, probeByPhone], Stubs.Matching(spec, probeByEmail, probeByPhone, probeOther));
    }

    [Theory]
    [InlineData("not-an-email", null, "Customers.EmailInvalid")]
    [InlineData(null, "0501234567", "Customers.PhoneInvalid")]
    public async Task AMalformedChannelIsA422BeforeAnyLookupOrNumber(string? email, string? phone, string code)
    {
        await Expect.DomainCode(() => Send(Command(contacts: [Contact("Sara", email, phone)])), code);

        await _harness.Repository.DidNotReceive().ListAsync(Arg.Any<Specification<Customer>>(), Arg.Any<CancellationToken>());
        await _numbers.DidNotReceive().NextAsync(Arg.Any<CancellationToken>());
    }

    // ---- aggregate rules reaching the handler ----

    [Fact]
    public async Task ADuplicateInsideTheRequestIsRefusedAfterTheNumberWasDrawn()
    {
        await Expect.DomainCode(
            () => Send(Command(confirm: true, contacts: [Contact("A", "x@example.com"), Contact("B", "x@example.com")])),
            "Customers.DuplicateContactEmail");

        // Pinned: the number is already drawn, so a refused create leaves a gap in the sequence.
        await _numbers.Received(1).NextAsync(Arg.Any<CancellationToken>());
        Assert.Empty(_harness.Log);
        Assert.Empty(_harness.Published);
    }

    [Fact]
    public async Task AnOrganisationWithoutALegalNameIsA422WithoutASave()
    {
        await Expect.DomainCode(() => Send(Command(type: "Organisation")), "Customers.LegalNameRequired");

        Assert.Empty(_harness.Log);
    }

    [Fact]
    public async Task AnIndividualWithALegalNameIsA422() =>
        await Expect.DomainCode(() => Send(Command(legalName: "Najd Co")), "Customers.LegalNameNotAllowed");

    [Fact]
    public async Task NoNameOnEitherSideIsA422() =>
        await Expect.DomainCode(() => Send(Command(ar: null, en: " ")), "Customers.NameRequired");

    [Fact]
    public async Task AnUnsupportedLanguageIsA422BeforeANumberIsDrawn()
    {
        await Expect.DomainCode(() => Send(Command(language: "fr")), "Customers.LanguageUnsupported");

        await _numbers.DidNotReceive().NextAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnUnknownTypeThatSkippedTheValidatorIsAnArgumentErrorNotA422() =>
        await Assert.ThrowsAsync<ArgumentException>(() => Send(Command(type: "Banana")));

    [Fact]
    public async Task ANumericTypeThatSkippedTheValidatorIsParsedAsTheEnumValue()
    {
        // Pinned: Enum.Parse accepts "1" as Organisation, which the validator's IsEnumName would have refused.
        Result<CreateCustomerOutcome> result = await Send(Command(type: "1", legalName: "Najd Co"));

        Assert.Equal("Organisation", result.Value.Customer?.Type);
    }

    [Fact]
    public void TheOutcomeOfADuplicateWarningHasAReadOnlyCandidateList()
    {
        CreateCustomerOutcome outcome = CreateCustomerOutcome.Duplicates([]);

        Assert.False(outcome.Created);
        Assert.Empty(outcome.DuplicateCandidates);
    }
}
