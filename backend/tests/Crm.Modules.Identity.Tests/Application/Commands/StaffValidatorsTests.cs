using Crm.Modules.Identity.Application.Commands.ChangePassword;
using Crm.Modules.Identity.Application.Commands.CreateUser;
using Crm.Modules.Identity.Application.Commands.DeactivateUser;
using Crm.Modules.Identity.Application.Commands.ReactivateUser;
using Crm.Modules.Identity.Application.Commands.RefreshSession;
using Crm.Modules.Identity.Application.Commands.ResetUserPassword;
using Crm.Modules.Identity.Application.Commands.SignIn;
using Crm.Modules.Identity.Application.Commands.UpdateUserProfile;
using Crm.Modules.Identity.Tests.Support;

namespace Crm.Modules.Identity.Tests.Application.Commands;

public sealed class SignInCommandValidatorTests
{
    private static readonly SignInCommandValidator Validator = new();

    [Fact]
    public void AValidCommandPasses() => Expect.Valid(Validator.Validate(new SignInCommand("a@b.co", "x")));

    [Fact]
    public void AnEmptyEmailFails() => Expect.Invalid(Validator.Validate(new SignInCommand("", "x")), "Email");

    [Fact]
    public void TheEmailLimitIs254()
    {
        Expect.Valid(Validator.Validate(new SignInCommand(new string('a', 254), "x")));
        Expect.Invalid(Validator.Validate(new SignInCommand(new string('a', 255), "x")), "Email");
    }

    [Fact]
    public void AnEmptyPasswordFails() => Expect.Invalid(Validator.Validate(new SignInCommand("a@b.co", "")), "Password");

    [Fact]
    public void ThePasswordLimitIs128()
    {
        Expect.Valid(Validator.Validate(new SignInCommand("a@b.co", new string('p', 128))));
        Expect.Invalid(Validator.Validate(new SignInCommand("a@b.co", new string('p', 129))), "Password");
    }

    [Fact]
    public void ThereIsNoFormatRuleOnEitherField() =>
        Expect.Valid(Validator.Validate(new SignInCommand("not-an-email", "x")));
}

public sealed class RefreshSessionCommandValidatorTests
{
    private static readonly RefreshSessionCommandValidator Validator = new();

    [Fact]
    public void AnEmptyTokenFails() => Expect.Invalid(Validator.Validate(new RefreshSessionCommand("")), "RefreshToken");

    [Fact]
    public void TheTokenLimitIs200()
    {
        Expect.Valid(Validator.Validate(new RefreshSessionCommand(new string('t', 200))));
        Expect.Invalid(Validator.Validate(new RefreshSessionCommand(new string('t', 201))), "RefreshToken");
    }
}

public sealed class ChangePasswordCommandValidatorTests
{
    private static readonly ChangePasswordCommandValidator Validator = new(FakeLocalization.Keys());

    [Fact]
    public void AValidCommandPasses() => Expect.Valid(Validator.Validate(new ChangePasswordCommand("old", "new-pass-1234")));

    [Fact]
    public void AnEmptyCurrentPasswordFails() => Expect.Invalid(Validator.Validate(new ChangePasswordCommand("", "new-pass-1234")), "CurrentPassword");

    [Fact]
    public void TheNewPasswordFollowsTheStaffRules()
    {
        Expect.Invalid(Validator.Validate(new ChangePasswordCommand("old", "short1")), "NewPassword");
        Expect.Invalid(Validator.Validate(new ChangePasswordCommand("old", "abcdefghijk")), "NewPassword");
        Expect.Invalid(Validator.Validate(new ChangePasswordCommand("old", "12345678901")), "NewPassword");
    }

    [Fact]
    public void ANewPasswordEqualToTheCurrentOneFailsOnTheNewPassword() =>
        Expect.Invalid(Validator.Validate(new ChangePasswordCommand("same-pass-12", "same-pass-12")), "NewPassword");
}

public sealed class CreateUserCommandValidatorTests
{
    private static readonly CreateUserCommandValidator Validator = new(FakeLocalization.Keys());

    private static CreateUserCommand Command(string email = "a@b.co", string name = "Name", string locale = "en", string password = "initial-pass-1") =>
        new(email, name, locale, password);

    [Fact]
    public void AValidCommandPasses() => Expect.Valid(Validator.Validate(Command()));

    [Theory]
    [InlineData("")]
    [InlineData("plain")]
    public void AnInvalidEmailFails(string email) => Expect.Invalid(Validator.Validate(Command(email: email)), "Email");

    [Fact]
    public void AnOverlongEmailFails() => Assert.False(Validator.Validate(Command(email: new string('a', 250) + "@b.co")).IsValid);

    [Fact]
    public void FluentValidationAcceptsAnEmailWithoutADotInTheDomainWhichTheDomainLaterRefuses() =>
        Expect.Valid(Validator.Validate(Command(email: "a@b")));

    [Fact]
    public void TheDisplayNameRuleIsEmptyAnd200()
    {
        Expect.Invalid(Validator.Validate(Command(name: "")), "DisplayName");
        Expect.Valid(Validator.Validate(Command(name: new string('n', 200))));
        Expect.Invalid(Validator.Validate(Command(name: new string('n', 201))), "DisplayName");
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ar")]
    public void SupportedLocalesPass(string locale) => Expect.Valid(Validator.Validate(Command(locale: locale)));

    [Theory]
    [InlineData("fr")]
    [InlineData("EN")]
    [InlineData("")]
    public void UnsupportedLocalesFail(string locale) => Expect.Invalid(Validator.Validate(Command(locale: locale)), "PreferredLocale");

    [Fact]
    public void TheInitialPasswordFollowsTheStaffRules()
    {
        Expect.Invalid(Validator.Validate(Command(password: "short1")), "InitialPassword");
        Expect.Invalid(Validator.Validate(Command(password: "abcdefghijk")), "InitialPassword");
        Expect.Invalid(Validator.Validate(Command(password: "")), "InitialPassword");
    }
}

public sealed class UpdateUserProfileCommandValidatorTests
{
    private static readonly UpdateUserProfileCommandValidator Validator = new(FakeLocalization.Keys());

    private static UpdateUserProfileCommand Command(Guid? id = null, string name = "Name", string locale = "en", byte[]? rowVersion = null) =>
        new(id ?? Ids.User, name, locale, rowVersion ?? Ids.RowVersion());

    [Fact]
    public void AValidCommandPasses() => Expect.Valid(Validator.Validate(Command()));

    [Fact]
    public void AnEmptyIdFails() => Expect.Invalid(Validator.Validate(Command(id: Guid.Empty)), "Id");

    [Fact]
    public void TheDisplayNameRuleIsEmptyAnd200()
    {
        Expect.Invalid(Validator.Validate(Command(name: "")), "DisplayName");
        Expect.Invalid(Validator.Validate(Command(name: new string('n', 201))), "DisplayName");
        Expect.Valid(Validator.Validate(Command(name: new string('n', 200))));
    }

    [Fact]
    public void AnUnsupportedLocaleFails() => Expect.Invalid(Validator.Validate(Command(locale: "fr")), "PreferredLocale");

    [Fact]
    public void AnEmptyRowVersionFails() => Expect.Invalid(Validator.Validate(Command(rowVersion: [])), "RowVersion");
}

public sealed class DeactivateUserCommandValidatorTests
{
    private static readonly DeactivateUserCommandValidator Validator = new();

    [Fact]
    public void AValidCommandPasses() => Expect.Valid(Validator.Validate(new DeactivateUserCommand(Ids.User, Ids.RowVersion())));

    [Fact]
    public void AnEmptyIdFails() => Expect.Invalid(Validator.Validate(new DeactivateUserCommand(Guid.Empty, Ids.RowVersion())), "Id");

    [Fact]
    public void AnEmptyRowVersionFails() => Expect.Invalid(Validator.Validate(new DeactivateUserCommand(Ids.User, [])), "RowVersion");
}

public sealed class ReactivateUserCommandValidatorTests
{
    private static readonly ReactivateUserCommandValidator Validator = new();

    [Fact]
    public void AValidCommandPasses() => Expect.Valid(Validator.Validate(new ReactivateUserCommand(Ids.User, Ids.RowVersion())));

    [Fact]
    public void AnEmptyIdFails() => Expect.Invalid(Validator.Validate(new ReactivateUserCommand(Guid.Empty, Ids.RowVersion())), "Id");

    [Fact]
    public void AnEmptyRowVersionFails() => Expect.Invalid(Validator.Validate(new ReactivateUserCommand(Ids.User, [])), "RowVersion");
}

public sealed class ResetUserPasswordCommandValidatorTests
{
    private static readonly ResetUserPasswordCommandValidator Validator = new(FakeLocalization.Keys());

    [Fact]
    public void AValidCommandPasses() => Expect.Valid(Validator.Validate(new ResetUserPasswordCommand(Ids.User, "new-pass-1234", Ids.RowVersion())));

    [Fact]
    public void AnEmptyIdFails() => Expect.Invalid(Validator.Validate(new ResetUserPasswordCommand(Guid.Empty, "new-pass-1234", Ids.RowVersion())), "Id");

    [Fact]
    public void TheNewPasswordFollowsTheStaffRules()
    {
        Expect.Invalid(Validator.Validate(new ResetUserPasswordCommand(Ids.User, "short1", Ids.RowVersion())), "NewPassword");
        Expect.Invalid(Validator.Validate(new ResetUserPasswordCommand(Ids.User, "abcdefghijk", Ids.RowVersion())), "NewPassword");
    }

    [Fact]
    public void AnEmptyRowVersionFails() => Expect.Invalid(Validator.Validate(new ResetUserPasswordCommand(Ids.User, "new-pass-1234", [])), "RowVersion");
}
