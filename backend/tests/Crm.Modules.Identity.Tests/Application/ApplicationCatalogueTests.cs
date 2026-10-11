using System.Globalization;
using System.Reflection;
using Crm.BuildingBlocks.Application.Authorization;
using Crm.Modules.Identity.Application;
using Crm.Modules.Identity.Application.Commands.ChangePassword;
using Crm.Modules.Identity.Application.Commands.ChangePortalPassword;
using Crm.Modules.Identity.Application.Commands.CreateUser;
using Crm.Modules.Identity.Application.Commands.DeactivateUser;
using Crm.Modules.Identity.Application.Commands.PortalSignIn;
using Crm.Modules.Identity.Application.Commands.PortalSignOut;
using Crm.Modules.Identity.Application.Commands.ProvisionPortalAccount;
using Crm.Modules.Identity.Application.Commands.ReactivateUser;
using Crm.Modules.Identity.Application.Commands.RefreshPortalSession;
using Crm.Modules.Identity.Application.Commands.RefreshSession;
using Crm.Modules.Identity.Application.Commands.RegisterPortalAccount;
using Crm.Modules.Identity.Application.Commands.ResetUserPassword;
using Crm.Modules.Identity.Application.Commands.SignIn;
using Crm.Modules.Identity.Application.Commands.SignOut;
using Crm.Modules.Identity.Application.Commands.UpdatePortalProfile;
using Crm.Modules.Identity.Application.Commands.UpdateUserProfile;
using Crm.Modules.Identity.Application.Localization;
using Crm.Modules.Identity.Application.Queries.GetMe;
using Crm.Modules.Identity.Application.Queries.GetPortalMe;
using Crm.Modules.Identity.Application.Queries.GetStaffUsers;
using Crm.Modules.Identity.Application.Queries.GetUser;
using Crm.Modules.Identity.Application.Queries.ListUsers;
using Crm.Modules.Identity.Domain;
using Crm.Modules.Identity.Tests.Support;
using FluentValidation;
using MediatR;

namespace Crm.Modules.Identity.Tests.Application;

public sealed class AuthorisationDeclarationTests
{
    public static TheoryData<Type, string> Declarations => new()
    {
        { typeof(SignInCommand), "anonymous" },
        { typeof(RefreshSessionCommand), "anonymous" },
        { typeof(SignOutCommand), "anonymous" },
        { typeof(PortalSignInCommand), "anonymous" },
        { typeof(RefreshPortalSessionCommand), "anonymous" },
        { typeof(PortalSignOutCommand), "anonymous" },
        { typeof(RegisterPortalAccountCommand), "anonymous" },
        { typeof(ChangePasswordCommand), "authenticated" },
        { typeof(GetMeQuery), "authenticated" },
        { typeof(GetStaffUsersQuery), "authenticated" },
        { typeof(CreateUserCommand), CrmPermissions.AdminUsers },
        { typeof(UpdateUserProfileCommand), CrmPermissions.AdminUsers },
        { typeof(DeactivateUserCommand), CrmPermissions.AdminUsers },
        { typeof(ReactivateUserCommand), CrmPermissions.AdminUsers },
        { typeof(ResetUserPasswordCommand), CrmPermissions.AdminUsers },
        { typeof(GetUserQuery), CrmPermissions.AdminUsers },
        { typeof(ListUsersQuery), CrmPermissions.AdminUsers },
        { typeof(ProvisionPortalAccountCommand), CrmPermissions.CustomersWrite },
        { typeof(ChangePortalPasswordCommand), CrmPermissions.PortalAccount },
        { typeof(UpdatePortalProfileCommand), CrmPermissions.PortalAccount },
        { typeof(GetPortalMeQuery), CrmPermissions.PortalAccount },
    };

    [Theory]
    [MemberData(nameof(Declarations))]
    public void EachRequestDeclaresItsAuthorisation(Type request, string expected)
    {
        string? permission = Stubs.PermissionOf(request);
        bool authenticated = Stubs.HasAttribute<RequireAuthenticatedUserAttribute>(request);
        bool anonymous = Stubs.HasAttribute<AllowAnonymousRequestAttribute>(request);

        Assert.Equal(1, (permission is null ? 0 : 1) + (authenticated ? 1 : 0) + (anonymous ? 1 : 0));
        Assert.Equal(expected, permission ?? (authenticated ? "authenticated" : "anonymous"));
    }

    [Fact]
    public void TheCataloguedRequestsAreTheWholeSet() =>
        Assert.Equal(21, Declarations.Count);
}

public sealed class ValidatorCatalogueTests
{
    private static readonly Assembly Application = AssemblyReference.Assembly;

    private static IEnumerable<Type> Requests() =>
        Application.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IBaseRequest).IsAssignableFrom(t));

    private static IEnumerable<Type> CommandTypes() =>
        Requests().Where(t => t.Namespace?.Contains(".Commands.", StringComparison.Ordinal) == true);

    private static Type? RequestOf(Type validator) =>
        validator.BaseType is { IsGenericType: true } b && b.GetGenericTypeDefinition() == typeof(AbstractValidator<>)
            ? b.GetGenericArguments()[0]
            : null;

    [Fact]
    public void ThereAreSixteenCommandsAndFiveQueries()
    {
        Assert.Equal(16, CommandTypes().Count());
        Assert.Equal(5, Requests().Count(t => t.Namespace?.Contains(".Queries.", StringComparison.Ordinal) == true));
    }

    [Fact]
    public void EveryCommandExceptTheTwoSignOutsHasExactlyOneValidator()
    {
        var validators = Application.GetTypes().Select(RequestOf).Where(r => r is not null).ToList();

        foreach (Type command in CommandTypes())
        {
            int count = validators.Count(r => r == command);
            if (command == typeof(SignOutCommand) || command == typeof(PortalSignOutCommand))
            {
                Assert.Equal(0, count);
            }
            else
            {
                Assert.True(count == 1, $"{command.Name} has {count} validators");
            }
        }
    }

    [Fact]
    public void NoValidatorIsDeclaredForAMissingRequest()
    {
        var requests = Requests().ToHashSet();

        foreach (Type validator in Application.GetTypes().Where(t => RequestOf(t) is not null))
        {
            Type request = RequestOf(validator) ?? throw new InvalidOperationException();
            Assert.Contains(request, requests);
        }
    }

    [Fact]
    public void TheQueriesWithValidatorsAreGetStaffUsersAndListUsers()
    {
        var queryValidators = Application.GetTypes()
            .Select(RequestOf)
            .Where(r => r is not null && r.Namespace?.Contains(".Queries.", StringComparison.Ordinal) == true)
            .ToList();

        Assert.Equal(2, queryValidators.Count);
        Assert.Contains(typeof(GetStaffUsersQuery), queryValidators);
        Assert.Contains(typeof(ListUsersQuery), queryValidators);
    }

    [Fact]
    public void EveryRequestDeclaresExactlyOneAuthorisationAttribute()
    {
        foreach (Type request in Requests())
        {
            int declared = new[]
            {
                request.GetCustomAttribute<RequirePermissionAttribute>() is not null,
                request.GetCustomAttribute<RequireAuthenticatedUserAttribute>() is not null,
                request.GetCustomAttribute<AllowAnonymousRequestAttribute>() is not null,
            }.Count(d => d);

            Assert.True(declared == 1, $"{request.Name} declares {declared} authorisation attributes");
        }
    }

    [Fact]
    public void EveryDeclaredPermissionIsInTheCatalogue()
    {
        foreach (string permission in Requests().Select(Stubs.PermissionOf).OfType<string>())
        {
            Assert.True(CrmPermissions.IsKnown(permission), permission);
        }
    }

    [Fact]
    public void TheModuleHasNoIntegrationEventsOrConsumers()
    {
        Assembly[] assemblies = [Application, typeof(User).Assembly];

        Assert.DoesNotContain(
            assemblies.SelectMany(a => a.GetTypes()),
            t => t.GetInterfaces().Any(i => i.Name.Contains("IIntegrationEvent", StringComparison.Ordinal)));
    }
}

public sealed class SessionSettingsTests
{
    [Fact]
    public void LockoutBuildsAPolicyFromTheSettings()
    {
        LockoutPolicy policy = new SessionSettings(TimeSpan.FromDays(1), 3, TimeSpan.FromMinutes(7)).Lockout;

        Assert.Equal(3, policy.MaxFailedAttempts);
        Assert.Equal(TimeSpan.FromMinutes(7), policy.Window);
    }

    [Fact]
    public void NonPositiveAttemptsThrow() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new SessionSettings(TimeSpan.FromDays(1), 0, TimeSpan.FromMinutes(7)).Lockout);

    [Fact]
    public void ANonPositiveWindowThrows() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new SessionSettings(TimeSpan.FromDays(1), 3, TimeSpan.Zero).Lockout);

    [Fact]
    public void EqualityIsByValue()
    {
        Assert.Equal(new SessionSettings(TimeSpan.FromDays(1), 3, TimeSpan.FromMinutes(7)), new SessionSettings(TimeSpan.FromDays(1), 3, TimeSpan.FromMinutes(7)));
        Assert.NotEqual(new SessionSettings(TimeSpan.FromDays(1), 3, TimeSpan.FromMinutes(7)), new SessionSettings(TimeSpan.FromDays(2), 3, TimeSpan.FromMinutes(7)));
    }
}

public sealed class IdentityMessageKeysTests
{
    public static TheoryData<string> Keys => new()
    {
        IdentityMessageKeys.Validation.PasswordNeedsLetter,
        IdentityMessageKeys.Validation.PasswordNeedsDigit,
        IdentityMessageKeys.Validation.LocaleUnsupported,
    };

    [Theory]
    [MemberData(nameof(Keys))]
    public void EveryKeyHasAnEnglishAndADifferentArabicValue(string key)
    {
        string en = FakeLocalization.Manager().GetString(key, CultureInfo.GetCultureInfo("en")) ?? string.Empty;
        string ar = FakeLocalization.Manager().GetString(key, CultureInfo.GetCultureInfo("ar")) ?? string.Empty;

        Assert.NotEmpty(en);
        Assert.NotEmpty(ar);
        Assert.NotEqual(en, ar);
    }

    [Fact]
    public void TheKeysAreTheResourceNames()
    {
        Assert.Equal("Identity.Validation.PasswordNeedsLetter", IdentityMessageKeys.Validation.PasswordNeedsLetter);
        Assert.Equal("Identity.Validation.PasswordNeedsDigit", IdentityMessageKeys.Validation.PasswordNeedsDigit);
        Assert.Equal("Identity.Validation.LocaleUnsupported", IdentityMessageKeys.Validation.LocaleUnsupported);
    }
}
