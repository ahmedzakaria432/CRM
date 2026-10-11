using Crm.Modules.Customers.Application;
using Crm.Modules.Customers.Application.Commands.UpdateContact;
using Crm.Modules.Customers.Domain;
using Crm.Modules.Customers.Tests.Support;
using Crm.SharedKernel.Results;
using FluentValidation.Results;

namespace Crm.Modules.Customers.Tests.Application.Commands.UpdateContact;

public sealed class UpdateContactCommandHandlerTests
{
    private readonly Harness _harness = new();

    private UpdateContactCommandHandler Handler() => new(_harness.Repository, _harness.UnitOfWork);

    private static UpdateContactCommand Command(
        Guid? contact = null, string name = "Renamed", string? email = "new@example.com", string? phone = null, bool portal = false) =>
        new(Ids.Customer1.Value, contact ?? Ids.Contact2, name, "Boss", email, phone, portal, Ids.RowVersion());

    private Customer Holding()
    {
        Customer customer = CustomerFixtures.Individual(id: Ids.Customer1);
        customer.AddSimpleContact("First", "first@example.com", "+966500000001", id: Ids.Contact1);
        customer.AddSimpleContact("Second", "second@example.com", "+966500000002", id: Ids.Contact2);
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
    public async Task AContactNotOnTheCustomerIsNotFoundBeforeTheRowVersionAndTheAggregate()
    {
        Holding();

        Result<CustomerDto> result = await Handler().Handle(Command(contact: Ids.Missing), Stubs.Ct);

        Expect.Failure(result, "Customers.ContactNotFound", ErrorType.NotFound);
        Assert.Contains(Ids.Missing.ToString(), result.Error.Description, StringComparison.Ordinal);
        Assert.Empty(_harness.Log);
    }

    [Fact]
    public async Task TheFieldsAreReplacedAfterTheRowVersionAndSavedOnce()
    {
        Customer customer = Holding();

        Result<CustomerDto> result = await Handler().Handle(Command(phone: "+966 50 000 0009", portal: true), Stubs.Ct);

        ContactDto contact = result.Value.Contacts.Single(c => c.Id == Ids.Contact2);
        Assert.Equal("Renamed", contact.Name);
        Assert.Equal("Boss", contact.JobTitle);
        Assert.Equal("new@example.com", contact.Email);
        Assert.Equal("+966500000009", contact.Phone);
        Assert.True(contact.HasPortalAccess);
        Assert.Equal(["rowversion", "save"], _harness.Log);
        _harness.Repository.Received(1).SetOriginalRowVersion(customer, Arg.Is<byte[]>(b => b.SequenceEqual(Ids.RowVersion())));
    }

    [Fact]
    public async Task KeepingItsOwnEmailIsAccepted()
    {
        Holding();

        Result<CustomerDto> result = await Handler().Handle(Command(email: "second@example.com"), Stubs.Ct);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task AnotherContactsEmailIsA422WithoutASave()
    {
        Holding();

        await Expect.DomainCode(() => Handler().Handle(Command(email: "first@example.com"), Stubs.Ct), "Customers.DuplicateContactEmail");

        Assert.Equal(0, _harness.Saves);
    }

    [Theory]
    [InlineData("nope", null, false, "Customers.EmailInvalid")]
    [InlineData(null, "0501234567", false, "Customers.PhoneInvalid")]
    [InlineData(null, null, false, "Customers.ContactChannelRequired")]
    [InlineData(null, "+966500000009", true, "Customers.PortalAccessRequiresEmail")]
    public async Task ChannelAndPortalRulesSurfaceAs422(string? email, string? phone, bool portal, string code)
    {
        Holding();

        await Expect.DomainCode(() => Handler().Handle(Command(email: email, phone: phone, portal: portal), Stubs.Ct), code);

        Assert.Equal(0, _harness.Saves);
    }

    [Fact]
    public void NoIntegrationEventIsPublishedBecauseTheHandlerHasNoBus() =>
        Assert.DoesNotContain(
            typeof(UpdateContactCommandHandler).GetConstructors().Single().GetParameters(),
            p => p.ParameterType == typeof(Crm.BuildingBlocks.Application.Messaging.IEventBus));
}

public sealed class UpdateContactCommandValidatorTests
{
    private readonly UpdateContactCommandValidator _validator = new();

    private ValidationResult Validate(
        Guid? customer = null, Guid? contact = null, string name = "Sara", string? title = null, string? email = "a@b.co",
        string? phone = null, byte[]? version = null) =>
        _validator.Validate(new UpdateContactCommand(
            customer ?? Ids.Customer1.Value, contact ?? Ids.Contact1, name, title, email, phone, false, version ?? Ids.RowVersion()));

    [Fact]
    public void ACompleteCommandIsValid() => Expect.Valid(Validate());

    [Fact]
    public void TheIdsAndTheRowVersionAreRequired()
    {
        Expect.Invalid(Validate(customer: Guid.Empty), "CustomerId");
        Expect.Invalid(Validate(contact: Guid.Empty), "ContactId");
        Expect.Invalid(Validate(version: []), "RowVersion");
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void TheNameIsRequiredAndLimitedToTwoHundredCharacters(int length, bool valid) =>
        Assert.Equal(valid, Validate(name: new string('n', length)).IsValid);

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void TheJobTitleLimitIsOneHundredCharacters(int length, bool valid) =>
        Assert.Equal(valid, Validate(title: new string('t', length)).IsValid);

    [Theory]
    [InlineData(254, true)]
    [InlineData(255, false)]
    public void TheEmailLimitIsByLengthOnly(int length, bool valid) =>
        Assert.Equal(valid, Validate(email: new string('e', length)).IsValid);

    [Theory]
    [InlineData(32, true)]
    [InlineData(33, false)]
    public void ThePhoneLimitIsByLengthOnly(int length, bool valid) =>
        Assert.Equal(valid, Validate(phone: new string('1', length)).IsValid);
}
