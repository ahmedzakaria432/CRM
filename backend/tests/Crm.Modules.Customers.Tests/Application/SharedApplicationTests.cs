using System.Globalization;
using Crm.BuildingBlocks.Application.Localization;
using Crm.Modules.Customers.Application;
using Crm.Modules.Customers.Application.Localization;
using Crm.Modules.Customers.Application.Validation;
using Crm.Modules.Customers.Tests.Support;
using FluentValidation;

namespace Crm.Modules.Customers.Tests.Application;

public sealed class CustomersRulesTests
{
    private sealed record Model(string Language, string? Sort);

    private static InlineValidator<Model> LanguageValidator(ILocalizationService localization)
    {
        var validator = new InlineValidator<Model>();
        validator.RuleFor(m => m.Language).SupportedLanguage(localization);
        return validator;
    }

    private static InlineValidator<Model> SortValidator(ILocalizationService localization)
    {
        var validator = new InlineValidator<Model>();
        validator.RuleFor(m => m.Sort).SupportedSort(localization);
        return validator;
    }

    [Theory]
    [InlineData("en", true)]
    [InlineData("ar", true)]
    [InlineData("EN", false)]
    [InlineData("fr", false)]
    [InlineData("", false)]
    [InlineData("en-US", false)]
    public void TheLanguageMustBeExactlyEnOrAr(string language, bool valid) =>
        Assert.Equal(valid, LanguageValidator(FakeLocalization.Keys()).Validate(new Model(language, null)).IsValid);

    [Theory]
    [InlineData(null, true)]
    [InlineData("createdAt", true)]
    [InlineData("CREATEDAT", true)]
    [InlineData("number", true)]
    [InlineData("nameEn", true)]
    [InlineData("NAMEAR", true)]
    [InlineData("name", false)]
    [InlineData("", false)]
    [InlineData("price", false)]
    public void TheSortMustBeAKnownKeyOrAbsent(string? sort, bool valid) =>
        Assert.Equal(valid, SortValidator(FakeLocalization.Keys()).Validate(new Model("en", sort)).IsValid);

    [Fact]
    public void TheMessagesAreTheLocalisedKeys()
    {
        Assert.Equal(
            CustomersMessageKeys.Validation.LanguageUnsupported,
            LanguageValidator(FakeLocalization.Keys()).Validate(new Model("fr", null)).Errors.Single().ErrorMessage);
        Assert.Equal(
            CustomersMessageKeys.Validation.SortUnsupported,
            SortValidator(FakeLocalization.Keys()).Validate(new Model("en", "price")).Errors.Single().ErrorMessage);
    }

    [Fact]
    public void TheEnglishMessagesComeFromTheResources()
    {
        using UiCulture culture = UiCulture.Use("en");

        Assert.Equal(
            "The preferred language must be en or ar.",
            LanguageValidator(FakeLocalization.Resources()).Validate(new Model("fr", null)).Errors.Single().ErrorMessage);
        Assert.Equal(
            "Sort must be one of createdAt, number, nameEn, nameAr.",
            SortValidator(FakeLocalization.Resources()).Validate(new Model("en", "price")).Errors.Single().ErrorMessage);
    }

    [Fact]
    public void TheArabicMessagesComeFromTheSatelliteResources()
    {
        using UiCulture culture = UiCulture.Use("ar");

        Assert.Equal(
            "يجب أن تكون اللغة المفضلة en أو ar.",
            LanguageValidator(FakeLocalization.Resources()).Validate(new Model("fr", null)).Errors.Single().ErrorMessage);
    }

    [Fact]
    public void BothRulesRejectNullArguments()
    {
        var validator = new InlineValidator<Model>();
        Func<IRuleBuilder<Model, string>, ILocalizationService, IRuleBuilderOptions<Model, string>> language = CustomersRules.SupportedLanguage;
        Func<IRuleBuilder<Model, string?>, ILocalizationService, IRuleBuilderOptions<Model, string?>> sort = CustomersRules.SupportedSort;

        NullArguments.AssertThrowsArgumentNull(language, null, FakeLocalization.Keys());
        NullArguments.AssertThrowsArgumentNull(language, validator.RuleFor(m => m.Language), null);
        NullArguments.AssertThrowsArgumentNull(sort, null, FakeLocalization.Keys());
        NullArguments.AssertThrowsArgumentNull(sort, validator.RuleFor(m => m.Sort), null);
    }
}

public sealed class CustomersMessageKeysTests
{
    [Theory]
    [InlineData(CustomersMessageKeys.Validation.NameRequired)]
    [InlineData(CustomersMessageKeys.Validation.LanguageUnsupported)]
    [InlineData(CustomersMessageKeys.Validation.SortUnsupported)]
    public void EveryKeyHasADifferentEnglishAndArabicText(string key)
    {
        var manager = FakeLocalization.Manager();

        string? english = manager.GetString(key, CultureInfo.GetCultureInfo("en"));
        string? arabic = manager.GetString(key, CultureInfo.GetCultureInfo("ar"));

        Assert.False(string.IsNullOrWhiteSpace(english));
        Assert.False(string.IsNullOrWhiteSpace(arabic));
        Assert.NotEqual(english, arabic);
    }

    [Fact]
    public void TheKeysAreNamedByAreaAndModule()
    {
        Assert.Equal("Customers.Validation.NameRequired", CustomersMessageKeys.Validation.NameRequired);
        Assert.Equal("Customers.Validation.LanguageUnsupported", CustomersMessageKeys.Validation.LanguageUnsupported);
        Assert.Equal("Customers.Validation.SortUnsupported", CustomersMessageKeys.Validation.SortUnsupported);
    }
}

public sealed class CustomersCacheKeysTests
{
    [Fact]
    public void TheCustomerKeyIsModuleAggregatePluralAndId()
    {
        var id = new Guid("11111111-2222-3333-4444-555555555555");

        Assert.Equal("customers:customers:11111111-2222-3333-4444-555555555555", CustomersCacheKeys.Customer(id));
    }

    [Fact]
    public void TheKeyIsDistinctPerIdAndStableAcrossCultures()
    {
        using UiCulture culture = UiCulture.Use("ar");

        Assert.NotEqual(CustomersCacheKeys.Customer(Ids.Customer1.Value), CustomersCacheKeys.Customer(Ids.Customer2.Value));
        Assert.Equal(CustomersCacheKeys.Customer(Ids.Customer1.Value), CustomersCacheKeys.Customer(Ids.Customer1.Value));
        Assert.Matches("^customers:customers:[0-9a-f-]{36}$", CustomersCacheKeys.Customer(Ids.Customer1.Value));
    }
}
