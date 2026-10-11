using Crm.Modules.Customers.Application;
using Crm.Modules.Customers.Application.Commands.AddContact;
using Crm.Modules.Customers.Domain;
using Crm.Modules.Customers.Tests.Support;
using Crm.SharedKernel.Results;
using FluentValidation.Results;

namespace Crm.Modules.Customers.Tests.Application.Commands.AddContact;

public sealed class AddContactCommandHandlerTests
{
    private readonly Harness _harness = new();

    private AddContactCommandHandler Handler() => new(_harness.Repository, _harness.UnitOfWork);

    private static AddContactCommand Command(
        string name = "Sara", string? email = "sara@example.com", string? phone = null, bool primary = false, bool portal = false) =>
        new(Ids.Customer1.Value, name, null, email, phone, primary, portal, Ids.RowVersion());

    private Customer Holding(int contacts = 0)
    {
        Customer customer = CustomerFixtures.Individual(id: Ids.Customer1);
        customer.WithContacts(contacts);
        _harness.Holding(customer);
        return customer;
    }

    [Fact]
    public async Task AnUnknownCustomerIsNotFound()
    {
        Result<CustomerDto> result = await Handler().Handle(Command(), Stubs.Ct);

        Expect.Failure(result, "Customers.CustomerNotFound", ErrorType.NotFound);
        Assert.Empty(_harness.Log);
    }

    [Fact]
    public async Task TheContactIsAddedAfterTheRowVersionAndTheFirstOneIsPrimary()
    {
        Customer customer = Holding();

        Result<CustomerDto> result = await Handler().Handle(Command(phone: "+966 50 000 0001"), Stubs.Ct);

        ContactDto contact = Assert.Single(result.Value.Contacts);
        Assert.Equal("Sara", contact.Name);
        Assert.Equal("+966500000001", contact.Phone);
        Assert.True(contact.IsPrimary);
        Assert.Equal(["rowversion", "save"], _harness.Log);
        _harness.Repository.Received(1).SetOriginalRowVersion(customer, Arg.Is<byte[]>(b => b.SequenceEqual(Ids.RowVersion())));
        Assert.Equal(1, customer.Revision);
    }

    [Fact]
    public async Task APrimaryContactDemotesTheOldOneAndComesFirstInTheDto()
    {
        Holding(contacts: 1);

        Result<CustomerDto> result = await Handler().Handle(Command("Zed", "zed@example.com", primary: true), Stubs.Ct);

        Assert.Equal(["Zed", "Contact 1"], result.Value.Contacts.Select(c => c.Name));
        Assert.Equal([true, false], result.Value.Contacts.Select(c => c.IsPrimary));
    }

    [Fact]
    public async Task NonPrimaryContactsAreOrderedByNameOrdinal()
    {
        Customer customer = Holding();
        customer.AddSimpleContact("Primary", "p@example.com");
        customer.AddSimpleContact("b", "b@example.com");
        customer.AddSimpleContact("B", "bb@example.com");

        Result<CustomerDto> result = await Handler().Handle(Command("a", "a@example.com"), Stubs.Ct);

        // Pinned: ordinal comparison puts every upper-case name before every lower-case one.
        Assert.Equal(["Primary", "B", "a", "b"], result.Value.Contacts.Select(c => c.Name));
    }

    [Fact]
    public void NoIntegrationEventIsPublishedBecauseTheHandlerHasNoBus() =>
        Assert.DoesNotContain(
            typeof(AddContactCommandHandler).GetConstructors().Single().GetParameters(),
            p => p.ParameterType == typeof(Crm.BuildingBlocks.Application.Messaging.IEventBus));

    [Theory]
    [InlineData("nope", null, "Customers.EmailInvalid")]
    [InlineData(null, "0501234567", "Customers.PhoneInvalid")]
    public async Task AMalformedChannelIsA422WithoutASave(string? email, string? phone, string code)
    {
        Holding();

        await Expect.DomainCode(() => Handler().Handle(Command(email: email, phone: phone), Stubs.Ct), code);

        Assert.Equal(0, _harness.Saves);
    }

    [Fact]
    public async Task ABlankEmailWithAValidPhoneIsFine()
    {
        Holding();

        Result<CustomerDto> result = await Handler().Handle(Command(email: "  ", phone: "+966500000001"), Stubs.Ct);

        Assert.Null(result.Value.Contacts[0].Email);
    }

    [Fact]
    public async Task NeitherChannelIsA422()
    {
        Holding();

        await Expect.DomainCode(() => Handler().Handle(Command(email: null, phone: null), Stubs.Ct), "Customers.ContactChannelRequired");
    }

    [Fact]
    public async Task PortalAccessWithoutAnEmailIsA422()
    {
        Holding();

        await Expect.DomainCode(
            () => Handler().Handle(Command(email: null, phone: "+966500000001", portal: true), Stubs.Ct), "Customers.PortalAccessRequiresEmail");
    }

    [Fact]
    public async Task ADuplicateEmailOrPhoneIsA422WithoutASave()
    {
        Holding(contacts: 1);

        await Expect.DomainCode(() => Handler().Handle(Command(email: "c1@example.com"), Stubs.Ct), "Customers.DuplicateContactEmail");
        await Expect.DomainCode(
            () => Handler().Handle(Command(email: "new@example.com", phone: "+966500000001"), Stubs.Ct), "Customers.DuplicateContactPhone");

        Assert.Equal(0, _harness.Saves);
    }
}

public sealed class AddContactCommandValidatorTests
{
    private readonly AddContactCommandValidator _validator = new();

    private ValidationResult Validate(
        Guid? customer = null, string name = "Sara", string? title = null, string? email = "a@b.co", string? phone = null, byte[]? version = null) =>
        _validator.Validate(new AddContactCommand(customer ?? Ids.Customer1.Value, name, title, email, phone, false, false, version ?? Ids.RowVersion()));

    [Fact]
    public void ACompleteCommandIsValid() => Expect.Valid(Validate());

    [Fact]
    public void TheCustomerAndTheRowVersionAreRequired()
    {
        Expect.Invalid(Validate(customer: Guid.Empty), "CustomerId");
        Expect.Invalid(Validate(version: []), "RowVersion");
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void TheNameIsRequiredAndLimitedToTwoHundredCharacters(object input, bool valid)
    {
        string name = input is int length ? new string('n', length) : (string)input;

        Assert.Equal(valid, Validate(name: name).IsValid);
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void TheJobTitleIsLimitedToOneHundredCharacters(int length, bool valid) =>
        Assert.Equal(valid, Validate(title: new string('t', length)).IsValid);

    [Theory]
    [InlineData(254, true)]
    [InlineData(255, false)]
    public void TheEmailIsLimitedByLengthOnly(int length, bool valid) =>
        Assert.Equal(valid, Validate(email: new string('e', length)).IsValid);

    [Theory]
    [InlineData(32, true)]
    [InlineData(33, false)]
    public void ThePhoneIsLimitedByLengthOnly(int length, bool valid) =>
        Assert.Equal(valid, Validate(phone: new string('1', length)).IsValid);

    [Fact]
    public void ThereAreNoFormatRules() => Expect.Valid(Validate(email: "not-an-email", phone: "abc"));
}
