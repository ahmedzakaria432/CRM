using Crm.Modules.Identity.Application.Commands.ChangePortalPassword;
using Crm.Modules.Identity.Application.Commands.PortalSignIn;
using Crm.Modules.Identity.Application.Commands.ProvisionPortalAccount;
using Crm.Modules.Identity.Application.Commands.RefreshPortalSession;
using Crm.Modules.Identity.Application.Commands.RegisterPortalAccount;
using Crm.Modules.Identity.Application.Commands.UpdatePortalProfile;
using Crm.Modules.Identity.Tests.Support;

namespace Crm.Modules.Identity.Tests.Application.Commands;

public sealed class PortalSignInCommandValidatorTests
{
    private static readonly PortalSignInCommandValidator Validator = new();

    [Fact]
    public void AValidCommandPasses() => Expect.Valid(Validator.Validate(new PortalSignInCommand("a@b.co", "x")));

    [Fact]
    public void AnEmptyEmailFails() => Expect.Invalid(Validator.Validate(new PortalSignInCommand("", "x")), "Email");

    [Fact]
    public void TheEmailLimitIs254()
    {
        Expect.Valid(Validator.Validate(new PortalSignInCommand(new string('a', 254), "x")));
        Expect.Invalid(Validator.Validate(new PortalSignInCommand(new string('a', 255), "x")), "Email");
    }

    [Fact]
    public void AnEmptyPasswordFails() => Expect.Invalid(Validator.Validate(new PortalSignInCommand("a@b.co", "")), "Password");

    [Fact]
    public void ThePasswordLimitIs128()
    {
        Expect.Valid(Validator.Validate(new PortalSignInCommand("a@b.co", new string('p', 128))));
        Expect.Invalid(Validator.Validate(new PortalSignInCommand("a@b.co", new string('p', 129))), "Password");
    }

    [Fact]
    public void ThereIsNoFormatRule() => Expect.Valid(Validator.Validate(new PortalSignInCommand("not-an-email", "x")));
}

public sealed class RefreshPortalSessionCommandValidatorTests
{
    private static readonly RefreshPortalSessionCommandValidator Validator = new();

    [Fact]
    public void AnEmptyTokenFails() => Expect.Invalid(Validator.Validate(new RefreshPortalSessionCommand("")), "RefreshToken");

    [Fact]
    public void TheTokenLimitIs200()
    {
        Expect.Valid(Validator.Validate(new RefreshPortalSessionCommand(new string('t', 200))));
        Expect.Invalid(Validator.Validate(new RefreshPortalSessionCommand(new string('t', 201))), "RefreshToken");
    }
}

public sealed class ChangePortalPasswordCommandValidatorTests
{
    private static readonly ChangePortalPasswordCommandValidator Validator = new(FakeLocalization.Keys());

    [Fact]
    public void AValidCommandPasses() => Expect.Valid(Validator.Validate(new ChangePortalPasswordCommand("old", "new-pass-1234")));

    [Fact]
    public void AnEmptyCurrentPasswordFails() => Expect.Invalid(Validator.Validate(new ChangePortalPasswordCommand("", "new-pass-1234")), "CurrentPassword");

    [Fact]
    public void TheNewPasswordFollowsTheStaffRules()
    {
        Expect.Invalid(Validator.Validate(new ChangePortalPasswordCommand("old", "short1")), "NewPassword");
        Expect.Invalid(Validator.Validate(new ChangePortalPasswordCommand("old", "abcdefghijk")), "NewPassword");
        Expect.Invalid(Validator.Validate(new ChangePortalPasswordCommand("old", "12345678901")), "NewPassword");
    }

    [Fact]
    public void ANewPasswordEqualToTheCurrentOneFails() =>
        Expect.Invalid(Validator.Validate(new ChangePortalPasswordCommand("same-pass-12", "same-pass-12")), "NewPassword");
}

public sealed class ProvisionPortalAccountCommandValidatorTests
{
    private static readonly ProvisionPortalAccountCommandValidator Validator = new(FakeLocalization.Keys());

    [Fact]
    public void AValidCommandPasses() => Expect.Valid(Validator.Validate(new ProvisionPortalAccountCommand(Ids.Customer, Ids.Contact, "portal-pass-123")));

    [Fact]
    public void AnEmptyCustomerFails() => Expect.Invalid(Validator.Validate(new ProvisionPortalAccountCommand(Guid.Empty, Ids.Contact, "portal-pass-123")), "CustomerId");

    [Fact]
    public void AnEmptyContactFails() => Expect.Invalid(Validator.Validate(new ProvisionPortalAccountCommand(Ids.Customer, Guid.Empty, "portal-pass-123")), "ContactId");

    [Fact]
    public void ThePasswordFollowsTheStaffRules()
    {
        Expect.Invalid(Validator.Validate(new ProvisionPortalAccountCommand(Ids.Customer, Ids.Contact, "short1")), "Password");
        Expect.Invalid(Validator.Validate(new ProvisionPortalAccountCommand(Ids.Customer, Ids.Contact, "abcdefghijk")), "Password");
        Expect.Invalid(Validator.Validate(new ProvisionPortalAccountCommand(Ids.Customer, Ids.Contact, "")), "Password");
    }
}

public sealed class RegisterPortalAccountCommandValidatorTests
{
    private static readonly RegisterPortalAccountCommandValidator Validator = new(FakeLocalization.Keys());

    private static RegisterPortalAccountCommand Command(string email = "a@b.co", string password = "portal-pass-123", string? locale = null) =>
        new(email, password, locale);

    [Fact]
    public void AValidCommandPasses() => Expect.Valid(Validator.Validate(Command()));

    [Fact]
    public void AnEmptyEmailFails() => Expect.Invalid(Validator.Validate(Command(email: "")), "Email");

    [Fact]
    public void TheEmailLimitIs254()
    {
        Expect.Valid(Validator.Validate(Command(email: new string('a', 254))));
        Expect.Invalid(Validator.Validate(Command(email: new string('a', 255))), "Email");
    }

    [Fact]
    public void ThePasswordFollowsTheStaffRules()
    {
        Expect.Invalid(Validator.Validate(Command(password: "short1")), "Password");
        Expect.Invalid(Validator.Validate(Command(password: "abcdefghijk")), "Password");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("en")]
    [InlineData("ar")]
    public void ANullOrSupportedLocalePasses(string? locale) => Expect.Valid(Validator.Validate(Command(locale: locale)));

    [Theory]
    [InlineData("fr")]
    [InlineData("")]
    [InlineData("EN")]
    public void AnUnsupportedLocaleFailsWithTheLocalisedMessage(string locale)
    {
        FluentValidation.Results.ValidationResult result = Validator.Validate(Command(locale: locale));

        Expect.Invalid(result, "PreferredLocale");
        Assert.Equal("Identity.Validation.LocaleUnsupported", Assert.Single(result.Errors).ErrorMessage);
    }
}

public sealed class UpdatePortalProfileCommandValidatorTests
{
    private static readonly UpdatePortalProfileCommandValidator Validator = new(FakeLocalization.Keys());

    [Fact]
    public void AValidCommandPasses() => Expect.Valid(Validator.Validate(new UpdatePortalProfileCommand("Name", "en")));

    [Fact]
    public void TheDisplayNameRuleIsEmptyAnd200()
    {
        Expect.Invalid(Validator.Validate(new UpdatePortalProfileCommand("", "en")), "DisplayName");
        Expect.Valid(Validator.Validate(new UpdatePortalProfileCommand(new string('n', 200), "en")));
        Expect.Invalid(Validator.Validate(new UpdatePortalProfileCommand(new string('n', 201), "en")), "DisplayName");
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ar")]
    public void SupportedLocalesPass(string locale) => Expect.Valid(Validator.Validate(new UpdatePortalProfileCommand("Name", locale)));

    [Fact]
    public void AnUnsupportedLocaleFails() => Expect.Invalid(Validator.Validate(new UpdatePortalProfileCommand("Name", "fr")), "PreferredLocale");
}
