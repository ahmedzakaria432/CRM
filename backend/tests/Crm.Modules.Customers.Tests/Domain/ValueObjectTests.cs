using Crm.Modules.Customers.Domain;
using Crm.Modules.Customers.Tests.Support;

namespace Crm.Modules.Customers.Tests.Domain;

public sealed class CustomerNumberTests
{
    [Theory]
    [InlineData(1, "C-000001")]
    [InlineData(123, "C-000123")]
    [InlineData(999999, "C-999999")]
    [InlineData(1000000, "C-1000000")]
    public void TheSequenceValueIsPaddedToSixDigits(long sequence, string expected) =>
        Assert.Equal(expected, CustomerNumber.FromSequence(sequence).Value);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void ANonPositiveSequenceValueIsRefused(long sequence) =>
        Expect.DomainCode(() => CustomerNumber.FromSequence(sequence), "Customers.NumberInvalid");

    [Fact]
    public void TheLargestSequenceValueOvershootsTheMaximumLengthAndIsStillAccepted()
    {
        // Pinned: C- plus 19 digits is 21 characters, one more than MaxLength (the column is 20). The sequence
        // never gets there in practice; the value object does not guard it.
        CustomerNumber number = CustomerNumber.FromSequence(long.MaxValue);

        Assert.Equal("C-9223372036854775807", number.Value);
        Assert.Equal(CustomerNumber.MaxLength + 1, number.Value.Length);
    }

    [Fact]
    public void TheDigitsAreAsciiUnderAnArabicCulture()
    {
        using UiCulture culture = UiCulture.Use("ar-SA");

        Assert.Equal("C-000042", CustomerNumber.FromSequence(42).Value);
    }

    [Fact]
    public void TheConstantsAreThePrefixAndTheMaximumLength()
    {
        Assert.Equal("C-", CustomerNumber.Prefix);
        Assert.Equal(20, CustomerNumber.MaxLength);
    }

    [Fact]
    public void NumbersWithTheSameValueAreEqual()
    {
        Assert.Equal(CustomerNumber.FromSequence(7), CustomerNumber.FromSequence(7));
        Assert.Equal(CustomerNumber.FromSequence(7).GetHashCode(), CustomerNumber.FromSequence(7).GetHashCode());
        Assert.NotEqual(CustomerNumber.FromSequence(7), CustomerNumber.FromSequence(8));
    }
}

public sealed class EmailAddressTests
{
    [Theory]
    [InlineData(" A@B.COM ", "a@b.com")]
    [InlineData("a@b.c", "a@b.c")]
    [InlineData("first.last@sub.example.co", "first.last@sub.example.co")]
    [InlineData("a+tag@x.io", "a+tag@x.io")]
    [InlineData("Mixed.Case@Example.ORG", "mixed.case@example.org")]
    public void AnAddressIsTrimmedAndLowerCased(string input, string expected) =>
        Assert.Equal(expected, EmailAddress.Create(input).Value);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("plain")]
    [InlineData("a@b")]
    [InlineData("a b@c.d")]
    [InlineData("a@b c.d")]
    [InlineData("@b.c")]
    [InlineData("a@@b.c")]
    [InlineData("a@b@c.d")]
    [InlineData("a@b.")]
    public void AMalformedAddressIsRefused(string input) =>
        Expect.DomainCode(() => EmailAddress.Create(input), "Customers.EmailInvalid");

    [Fact]
    public void TheLengthLimitIs254Characters()
    {
        string accepted = new string('a', 254 - "@x.io".Length) + "@x.io";
        string rejected = new string('a', 255 - "@x.io".Length) + "@x.io";

        Assert.Equal(254, EmailAddress.Create(accepted).Value.Length);
        Expect.DomainCode(() => EmailAddress.Create(rejected), "Customers.EmailInvalid");
        Assert.Equal(254, EmailAddress.MaxLength);
    }

    [Fact]
    public void ANullValueIsAnArgumentError() =>
        NullArguments.AssertThrowsArgumentNull(EmailAddress.Create, [null]);

    [Fact]
    public void TryCreateNeverThrowsAndNormalises()
    {
        Assert.False(EmailAddress.TryCreate(null, out EmailAddress? none));
        Assert.Null(none);

        Assert.False(EmailAddress.TryCreate("not-an-email", out EmailAddress? bad));
        Assert.Null(bad);

        Assert.True(EmailAddress.TryCreate(" A@B.COM ", out EmailAddress? good));
        Assert.Equal("a@b.com", good.Value);
    }

    [Fact]
    public void AddressesAreEqualByTheirNormalisedValue()
    {
        Assert.Equal(EmailAddress.Create("A@X.example"), EmailAddress.Create(" a@x.example "));
        Assert.Equal(EmailAddress.Create("A@X.example").GetHashCode(), EmailAddress.Create("a@x.example").GetHashCode());
        Assert.NotEqual(EmailAddress.Create("a@x.example"), EmailAddress.Create("b@x.example"));
    }
}

public sealed class PhoneNumberTests
{
    [Theory]
    [InlineData("+966501234567", "+966501234567")]
    [InlineData("+966 50 123 4567", "+966501234567")]
    [InlineData("+966-50-123-4567", "+966501234567")]
    [InlineData("+(966) 50.123.4567", "+966501234567")]
    [InlineData("  +966501234567  ", "+966501234567")]
    public void SeparatorsAreStrippedAndTheValueIsKept(string input, string expected) =>
        Assert.Equal(expected, PhoneNumber.Create(input).Value);

    [Theory]
    [InlineData("0501234567")]
    [InlineData("966501234567")]
    [InlineData("+0966501234567")]
    [InlineData("+966abc")]
    [InlineData("+1")]
    [InlineData("+")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("+1234567890123456")]
    public void ANumberThatIsNotE164IsRefused(string input) =>
        Expect.DomainCode(() => PhoneNumber.Create(input), "Customers.PhoneInvalid");

    [Theory]
    [InlineData("+12")]
    [InlineData("+123456789012345")]
    public void TheDigitCountRangeIsTwoToFifteen(string input) =>
        Assert.Equal(input, PhoneNumber.Create(input).Value);

    [Fact]
    public void ArabicIndicDigitsAfterAnAsciiLeadingDigitAreAcceptedBecauseDigitMatchesUnicode()
    {
        // Pinned: the regex uses \d, which matches every Unicode decimal digit in .NET. Only the first digit is [1-9].
        const string input = "+966٥٠١٢٣٤٥٦٧";

        Assert.True(PhoneNumber.TryCreate(input, out PhoneNumber? phone));
        Assert.Equal(input, phone.Value);
    }

    [Fact]
    public void AnArabicIndicLeadingDigitIsRefused() =>
        Assert.False(PhoneNumber.TryCreate("+٩٦٦٥٠", out _));

    [Fact]
    public void ANullValueIsAnArgumentErrorOnCreateAndFalseOnTryCreate()
    {
        NullArguments.AssertThrowsArgumentNull(PhoneNumber.Create, [null]);

        Assert.False(PhoneNumber.TryCreate(null, out PhoneNumber? phone));
        Assert.Null(phone);
    }

    [Fact]
    public void TryCreateReportsAMalformedNumberWithoutThrowing()
    {
        Assert.False(PhoneNumber.TryCreate("0501234567", out PhoneNumber? phone));
        Assert.Null(phone);
        Assert.Equal(16, PhoneNumber.MaxLength);
    }

    [Theory]
    [InlineData("+966 50 123", "+96650123")]
    [InlineData("  (05) 12-34.5 ", "0512345")]
    [InlineData("- ( )", "")]
    [InlineData("", "")]
    [InlineData("abc", "abc")]
    public void StripSeparatorsRemovesSeparatorsAndDoesNotValidate(string input, string expected) =>
        Assert.Equal(expected, PhoneNumber.StripSeparators(input));

    [Fact]
    public void StripSeparatorsRejectsNull() =>
        NullArguments.AssertThrowsArgumentNull(PhoneNumber.StripSeparators, [null]);

    [Fact]
    public void NumbersAreEqualByTheirNormalisedValue()
    {
        Assert.Equal(PhoneNumber.Create("+966 55 111 2233"), PhoneNumber.Create("+966551112233"));
        Assert.NotEqual(PhoneNumber.Create("+966551112233"), PhoneNumber.Create("+966551112234"));
    }
}

public sealed class LocalizedTextTests
{
    [Fact]
    public void BothSidesAreTrimmedAndKept()
    {
        LocalizedText text = LocalizedText.Create("  أحمد  ", "  Ahmed ");

        Assert.Equal("أحمد", text.Arabic);
        Assert.Equal("Ahmed", text.English);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AMissingArabicSideTakesTheEnglishText(string? arabic)
    {
        LocalizedText text = LocalizedText.Create(arabic, "Ahmed");

        Assert.Equal("Ahmed", text.Arabic);
        Assert.Equal("Ahmed", text.English);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AMissingEnglishSideTakesTheArabicText(string? english)
    {
        LocalizedText text = LocalizedText.Create("أحمد", english);

        Assert.Equal("أحمد", text.Arabic);
        Assert.Equal("أحمد", text.English);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", " ")]
    [InlineData("   ", null)]
    public void BothSidesMissingIsRefused(string? arabic, string? english) =>
        Expect.DomainCode(() => LocalizedText.Create(arabic, english), "Customers.NameRequired");

    [Fact]
    public void TwoHundredCharactersAreAcceptedAndTwoHundredAndOneAreNot()
    {
        Assert.Equal(200, LocalizedText.Create(new string('a', 200), null).English.Length);
        Assert.Equal(200, LocalizedText.Create(null, new string('b', 200)).Arabic.Length);
        Expect.DomainCode(() => LocalizedText.Create(new string('a', 201), "ok"), "Customers.NameTooLong");
        Expect.DomainCode(() => LocalizedText.Create("ok", new string('b', 201)), "Customers.NameTooLong");
        Assert.Equal(200, LocalizedText.MaxLength);
    }

    [Fact]
    public void TheLengthIsCheckedAfterTheTrim() =>
        Assert.Equal(200, LocalizedText.Create("  " + new string('a', 200) + "  ", null).Arabic.Length);

    [Fact]
    public void ATooLongArabicSideIsNotRescuedByAnEmptyEnglishSide() =>
        Expect.DomainCode(() => LocalizedText.Create(new string('a', 201), null), "Customers.NameTooLong");

    [Fact]
    public void TextsAreEqualWhenBothSidesAreEqual()
    {
        Assert.Equal(LocalizedText.Create("a", "b"), LocalizedText.Create(" a ", "b"));
        Assert.Equal(LocalizedText.Create("a", "b").GetHashCode(), LocalizedText.Create("a", "b").GetHashCode());
        Assert.NotEqual(LocalizedText.Create("a", "b"), LocalizedText.Create("a", "c"));
        Assert.NotEqual(LocalizedText.Create("a", "b"), LocalizedText.Create("c", "b"));
    }
}

public sealed class PreferredLanguageTests
{
    [Theory]
    [InlineData("en", "en")]
    [InlineData("EN", "en")]
    [InlineData(" ar ", "ar")]
    [InlineData("Ar", "ar")]
    public void ASupportedLanguageIsNormalised(string input, string expected) =>
        Assert.Equal(expected, PreferredLanguage.Create(input).Value);

    [Fact]
    public void TheValuesAreTheSingletons()
    {
        Assert.Same(PreferredLanguage.English, PreferredLanguage.Create("EN"));
        Assert.Same(PreferredLanguage.Arabic, PreferredLanguage.Create(" ar "));
        Assert.Equal("en", PreferredLanguage.English.Value);
        Assert.Equal("ar", PreferredLanguage.Arabic.Value);
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("")]
    [InlineData("en-US")]
    [InlineData("   ")]
    public void AnUnsupportedLanguageIsRefused(string input) =>
        Expect.DomainCode(() => PreferredLanguage.Create(input), "Customers.LanguageUnsupported");

    [Fact]
    public void ANullLanguageIsAnArgumentError() =>
        NullArguments.AssertThrowsArgumentNull(PreferredLanguage.Create, [null]);

    [Fact]
    public void LanguagesAreEqualByValue()
    {
        Assert.Equal(PreferredLanguage.English, PreferredLanguage.Create("en"));
        Assert.NotEqual(PreferredLanguage.English, PreferredLanguage.Arabic);
    }
}

public sealed class CustomerIdTests
{
    [Fact]
    public void NewIdsAreNonEmptyAndDistinct()
    {
        CustomerId first = CustomerId.New();
        CustomerId second = CustomerId.New();

        Assert.NotEqual(Guid.Empty, first.Value);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void IdsAreEqualByValue()
    {
        var value = new Guid("11111111-1111-1111-1111-111111111111");

        Assert.Equal(new CustomerId(value), new CustomerId(value));
        Assert.Equal(new CustomerId(value).GetHashCode(), new CustomerId(value).GetHashCode());
    }

    [Fact]
    public void AnEmptyGuidIsNotGuarded() =>
        Assert.Equal(Guid.Empty, new CustomerId(Guid.Empty).Value);
}

public sealed class EnumContractTests
{
    [Fact]
    public void CustomerTypeNamesAndOrderAreTheStorageContract() =>
        Assert.Equal(["Individual", "Organisation"], Enum.GetNames<CustomerType>());

    [Fact]
    public void CustomerStatusNamesAndOrderAreTheStorageContract() =>
        Assert.Equal(["Active", "Inactive"], Enum.GetNames<CustomerStatus>());
}
