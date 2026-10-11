using Crm.Modules.Customers.Application.Commands.CreateCustomer;
using Crm.Modules.Customers.Tests.Support;
using FluentValidation.Results;

namespace Crm.Modules.Customers.Tests.Application.Commands.CreateCustomer;

public sealed class CreateCustomerCommandValidatorTests
{
    private readonly CreateCustomerCommandValidator _validator = new(FakeLocalization.Keys());

    private static CreateCustomerCommand Command(
        string? ar = "أحمد", string? en = "Ahmed", string type = "Individual", string? legalName = null,
        string language = "en", IReadOnlyList<NewContact>? contacts = null) =>
        new(ar, en, type, legalName, language, contacts ?? [], false, null);

    private ValidationResult Validate(CreateCustomerCommand command) => _validator.Validate(command);

    private static NewContact Contact(string name = "Sara", string? title = null, string? email = null, string? phone = null) =>
        new(name, title, email, phone, false, false);

    [Fact]
    public void ACompleteCommandIsValid() => Expect.Valid(Validate(Command()));

    [Fact]
    public void EnglishIsRequiredOnlyWhenArabicIsBlank()
    {
        Expect.Invalid(Validate(Command(ar: null, en: null)), "DisplayNameEn");
        Expect.Invalid(Validate(Command(ar: "   ", en: "")), "DisplayNameEn");
        Expect.Valid(Validate(Command(ar: "أحمد", en: null)));
        Expect.Valid(Validate(Command(ar: null, en: "Ahmed")));
    }

    [Fact]
    public void ThePropertyMessageIsTheLocalisedNameRequiredKey()
    {
        ValidationResult result = Validate(Command(ar: null, en: null));

        Assert.Equal("Customers.Validation.NameRequired", Assert.Single(result.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void EachNameSideHasItsOwnTwoHundredCharacterLimit(int length, bool valid)
    {
        string text = new('a', length);

        ValidationResult arabic = Validate(Command(ar: text, en: "ok"));
        ValidationResult english = Validate(Command(ar: "ok", en: text));

        if (valid)
        {
            Expect.Valid(arabic);
            Expect.Valid(english);
        }
        else
        {
            Expect.Invalid(arabic, "DisplayNameAr");
            Expect.Invalid(english, "DisplayNameEn");
        }
    }

    [Theory]
    [InlineData("Individual", true)]
    [InlineData("Organisation", true)]
    [InlineData("organisation", true)]
    [InlineData("INDIVIDUAL", true)]
    [InlineData("", false)]
    [InlineData("Banana", false)]
    [InlineData("1", false)]
    public void TheTypeMustBeANameOfTheEnum(string type, bool valid)
    {
        ValidationResult result = Validate(Command(type: type));

        if (valid)
        {
            Expect.Valid(result);
        }
        else
        {
            Assert.False(result.IsValid);
            Assert.All(result.Errors, e => Assert.Equal("Type", e.PropertyName));
        }
    }

    [Theory]
    [InlineData(300, true)]
    [InlineData(301, false)]
    public void TheLegalNameLimitIsThreeHundredCharacters(int length, bool valid)
    {
        ValidationResult result = Validate(Command(type: "Organisation", legalName: new string('n', length)));

        if (valid)
        {
            Expect.Valid(result);
        }
        else
        {
            Expect.Invalid(result, "LegalName");
        }
    }

    [Fact]
    public void ThereIsNoLegalNameRequiredRuleBecauseTheAggregateOwnsIt() =>
        Expect.Valid(Validate(Command(type: "Organisation", legalName: null)));

    [Theory]
    [InlineData("en", true)]
    [InlineData("ar", true)]
    [InlineData("EN", false)]
    [InlineData("fr", false)]
    [InlineData("", false)]
    public void ThePreferredLanguageIsEnOrAr(string language, bool valid)
    {
        ValidationResult result = Validate(Command(language: language));

        if (valid)
        {
            Expect.Valid(result);
        }
        else
        {
            Expect.Invalid(result, "PreferredLanguage");
            Assert.Equal("Customers.Validation.LanguageUnsupported", result.Errors[0].ErrorMessage);
        }
    }

    [Fact]
    public void ANullContactListMakesTheValidatorThrowRatherThanFail()
    {
        // Pinned: NotNull().Must(...) does not stop at NotNull, so the Must predicate dereferences null.
        // A literal null does not compile warning-free for a non-nullable parameter, so build the command by reflection.
        var command = (CreateCustomerCommand)typeof(CreateCustomerCommand).GetConstructors()[0]
            .Invoke(["Ahmed", "Ahmed", "Individual", null, "en", null, false, null]);

        Assert.Throws<NullReferenceException>(() => Validate(command));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(20, true)]
    [InlineData(21, false)]
    public void AtMostTwentyContactsAreAccepted(int count, bool valid)
    {
        NewContact[] contacts = [.. Enumerable.Range(1, count).Select(n => Contact($"C{n}", email: $"c{n}@example.com"))];

        ValidationResult result = Validate(Command(contacts: contacts));

        if (valid)
        {
            Expect.Valid(result);
        }
        else
        {
            Expect.Invalid(result, "Contacts");
        }
    }

    [Fact]
    public void AContactNameIsRequired()
    {
        Expect.Invalid(Validate(Command(contacts: [Contact(name: "")])), "Contacts[0].Name");
        Expect.Invalid(Validate(Command(contacts: [Contact("ok"), Contact(name: "")])), "Contacts[1].Name");
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void AContactNameLimitIsTwoHundredCharacters(int length, bool valid)
    {
        ValidationResult result = Validate(Command(contacts: [Contact(new string('n', length))]));

        if (valid)
        {
            Expect.Valid(result);
        }
        else
        {
            Expect.Invalid(result, "Contacts[0].Name");
        }
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void AContactJobTitleLimitIsOneHundredCharacters(int length, bool valid)
    {
        ValidationResult result = Validate(Command(contacts: [Contact(title: new string('t', length))]));

        if (valid)
        {
            Expect.Valid(result);
        }
        else
        {
            Expect.Invalid(result, "Contacts[0].JobTitle");
        }
    }

    [Theory]
    [InlineData(254, true)]
    [InlineData(255, false)]
    public void AContactEmailLimitIsTheValueObjectLimit(int length, bool valid)
    {
        ValidationResult result = Validate(Command(contacts: [Contact(email: new string('e', length))]));

        if (valid)
        {
            Expect.Valid(result);
        }
        else
        {
            Expect.Invalid(result, "Contacts[0].Email");
        }
    }

    [Theory]
    [InlineData(32, true)]
    [InlineData(33, false)]
    public void AContactPhoneLimitIsThirtyTwoCharacters(int length, bool valid)
    {
        ValidationResult result = Validate(Command(contacts: [Contact(phone: new string('1', length))]));

        if (valid)
        {
            Expect.Valid(result);
        }
        else
        {
            Expect.Invalid(result, "Contacts[0].Phone");
        }
    }

    [Fact]
    public void ThereIsNoEmailOrPhoneFormatRule() =>
        Expect.Valid(Validate(Command(contacts: [Contact(email: "not-an-email", phone: "abc")])));
}
