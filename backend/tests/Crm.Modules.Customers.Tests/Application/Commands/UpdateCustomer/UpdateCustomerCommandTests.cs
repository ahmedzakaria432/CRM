using Crm.Modules.Customers.Application;
using Crm.Modules.Customers.Application.Commands.UpdateCustomer;
using Crm.Modules.Customers.Contracts.IntegrationEvents;
using Crm.Modules.Customers.Domain;
using Crm.Modules.Customers.Tests.Support;
using Crm.SharedKernel.Results;
using FluentValidation.Results;

namespace Crm.Modules.Customers.Tests.Application.Commands.UpdateCustomer;

public sealed class UpdateCustomerCommandHandlerTests
{
    private readonly Harness _harness = new();

    private UpdateCustomerCommandHandler Handler() => new(_harness.Repository, _harness.UnitOfWork, _harness.Clock, _harness.Bus);

    private static UpdateCustomerCommand Command(
        string? ar = "سارة", string? en = "Sara", string type = "Individual", string? legalName = null,
        string language = "ar") =>
        new(Ids.Customer1.Value, ar, en, type, legalName, language, Ids.RowVersion());

    [Fact]
    public async Task AnUnknownCustomerIsNotFoundAndNothingHappens()
    {
        Result<CustomerDto> result = await Handler().Handle(Command(), Stubs.Ct);

        Expect.Failure(result, "Customers.CustomerNotFound", ErrorType.NotFound);
        Assert.Contains(Ids.Customer1.Value.ToString(), result.Error.Description, StringComparison.Ordinal);
        Assert.Empty(_harness.Log);
        Assert.Empty(_harness.Published);
    }

    [Fact]
    public async Task TheRowVersionIsAppliedBeforeTheChangeAndTheProfileIsReplaced()
    {
        Customer customer = CustomerFixtures.Individual(id: Ids.Customer1);
        _harness.Holding(customer);

        Result<CustomerDto> result = await Handler().Handle(Command(), Stubs.Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(["rowversion", "save", "publish"], _harness.Log);
        _harness.Repository.Received(1).SetOriginalRowVersion(customer, Arg.Is<byte[]>(b => b.SequenceEqual(Ids.RowVersion())));
        Assert.Equal("Sara", result.Value.DisplayName.En);
        Assert.Equal("ar", result.Value.PreferredLanguage);
        Assert.Equal(1, customer.Revision);
    }

    [Fact]
    public async Task TheUpdatedEventIsPublishedAfterTheSaveWithTheNewValues()
    {
        _harness.Holding(CustomerFixtures.Individual(id: Ids.Customer1));

        await Handler().Handle(Command(type: "organisation", legalName: "Najd Co"), Stubs.Ct);

        CustomerUpdatedIntegrationEvent published = Assert.IsType<CustomerUpdatedIntegrationEvent>(Assert.Single(_harness.Published));
        Assert.Equal(Ids.Customer1.Value, published.CustomerId);
        Assert.Equal("سارة", published.DisplayNameAr);
        Assert.Equal("Sara", published.DisplayNameEn);
        Assert.Equal("Organisation", published.Type);
        Assert.Equal("Active", published.Status);
        Assert.Equal(Clock.Now, published.OccurredOnUtc);
        Assert.NotEqual(Guid.Empty, published.Id);
        Assert.Equal("publish", _harness.Log[^1]);
    }

    [Fact]
    public async Task AnOrganisationBecomesAnIndividualWhenTheLegalNameIsCleared()
    {
        _harness.Holding(CustomerFixtures.Organisation(id: Ids.Customer1));

        Result<CustomerDto> result = await Handler().Handle(Command(type: "Individual", legalName: null), Stubs.Ct);

        Assert.Equal("Individual", result.Value.Type);
        Assert.Null(result.Value.LegalName);
    }

    [Fact]
    public async Task ALegalNameOnAnIndividualIsA422WithoutASaveOrAnEvent()
    {
        _harness.Holding(CustomerFixtures.Organisation(id: Ids.Customer1));

        await Expect.DomainCode(() => Handler().Handle(Command(type: "Individual", legalName: "Najd Co"), Stubs.Ct), "Customers.LegalNameNotAllowed");

        Assert.Equal(["rowversion"], _harness.Log);
        Assert.Empty(_harness.Published);
    }

    [Fact]
    public async Task AnInvalidLanguageIsA422()
    {
        _harness.Holding(CustomerFixtures.Individual(id: Ids.Customer1));

        await Expect.DomainCode(() => Handler().Handle(Command(language: "fr"), Stubs.Ct), "Customers.LanguageUnsupported");

        Assert.Equal(0, _harness.Saves);
    }

    [Fact]
    public async Task AMissingNameOnBothSidesIsA422()
    {
        _harness.Holding(CustomerFixtures.Individual(id: Ids.Customer1));

        await Expect.DomainCode(() => Handler().Handle(Command(ar: null, en: null), Stubs.Ct), "Customers.NameRequired");

        Assert.Empty(_harness.Published);
    }

    [Fact]
    public async Task AnInactiveCustomerStaysInactiveAndTheEventCarriesIt()
    {
        Customer customer = CustomerFixtures.Individual(id: Ids.Customer1);
        customer.Deactivate();
        _harness.Holding(customer);

        Result<CustomerDto> result = await Handler().Handle(Command(), Stubs.Ct);

        Assert.Equal("Inactive", result.Value.Status);
        Assert.Equal("Inactive", Assert.IsType<CustomerUpdatedIntegrationEvent>(Assert.Single(_harness.Published)).Status);
    }
}

public sealed class UpdateCustomerCommandValidatorTests
{
    private readonly UpdateCustomerCommandValidator _validator = new(FakeLocalization.Keys());

    private ValidationResult Validate(
        Guid? id = null, string? ar = "أ", string? en = "A", string type = "Individual", string? legalName = null,
        string language = "en", byte[]? rowVersion = null) =>
        _validator.Validate(new UpdateCustomerCommand(id ?? Ids.Customer1.Value, ar, en, type, legalName, language, rowVersion ?? Ids.RowVersion()));

    [Fact]
    public void ACompleteCommandIsValid() => Expect.Valid(Validate());

    [Fact]
    public void TheIdIsRequired() => Expect.Invalid(Validate(id: Guid.Empty), "Id");

    [Fact]
    public void EnglishIsRequiredOnlyWhenArabicIsBlank()
    {
        Expect.Invalid(Validate(ar: null, en: null), "DisplayNameEn");
        Expect.Valid(Validate(ar: null, en: "A"));
        Expect.Valid(Validate(ar: "أ", en: null));
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void EachNameSideIsLimitedToTwoHundredCharacters(int length, bool valid)
    {
        Assert.Equal(valid, Validate(ar: new string('a', length), en: "ok").IsValid);
        Assert.Equal(valid, Validate(ar: "ok", en: new string('a', length)).IsValid);
    }

    [Theory]
    [InlineData("Individual", true)]
    [InlineData("organisation", true)]
    [InlineData("", false)]
    [InlineData("1", false)]
    [InlineData("Banana", false)]
    public void TheTypeMustBeAnEnumName(string type, bool valid) =>
        Assert.Equal(valid, Validate(type: type).IsValid);

    [Theory]
    [InlineData(300, true)]
    [InlineData(301, false)]
    public void TheLegalNameIsLimitedToThreeHundredCharacters(int length, bool valid) =>
        Assert.Equal(valid, Validate(type: "Organisation", legalName: new string('n', length)).IsValid);

    [Theory]
    [InlineData("en", true)]
    [InlineData("ar", true)]
    [InlineData("fr", false)]
    [InlineData("EN", false)]
    public void TheLanguageIsEnOrAr(string language, bool valid) =>
        Assert.Equal(valid, Validate(language: language).IsValid);

    [Fact]
    public void AnEmptyRowVersionIsInvalid() => Expect.Invalid(Validate(rowVersion: []), "RowVersion");

    [Fact]
    public void ANullRowVersionIsInvalidRatherThanThrowing()
    {
        var command = (UpdateCustomerCommand)typeof(UpdateCustomerCommand).GetConstructors()[0]
            .Invoke([Ids.Customer1.Value, "a", "a", "Individual", null, "en", null]);

        Expect.Invalid(_validator.Validate(command), "RowVersion");
    }
}
