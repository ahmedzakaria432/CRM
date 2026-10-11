using System.Globalization;
using Crm.BuildingBlocks.Application.Abstractions;
using Crm.BuildingBlocks.Application.Localization;
using Crm.BuildingBlocks.Application.Modules;
using Crm.BuildingBlocks.Infrastructure.Modules;
using Crm.BuildingBlocks.Infrastructure.Persistence.Repositories;
using Crm.Modules.Identity.Application;
using Crm.Modules.Identity.Application.Abstractions;
using Crm.Modules.Identity.Application.Localization;
using Crm.Modules.Identity.Contracts;
using Crm.Modules.Identity.Domain;
using Crm.Modules.Identity.Infrastructure;
using Crm.Modules.Identity.Infrastructure.Persistence;
using Crm.Modules.Identity.Infrastructure.Security;
using Crm.Modules.Identity.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crm.Modules.Identity.Tests.Infrastructure;

/// <summary>Inspects the registrations only: nothing resolves the DbContext, so nothing opens SQL Server.</summary>
public sealed class IdentityModuleTests
{
    private static ServiceCollection Registered(Action<Dictionary<string, string?>>? tweak = null)
    {
        Dictionary<string, string?> settings = JwtSettingsFixture.Configuration();
        tweak?.Invoke(settings);
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        new IdentityModule().AddServices(services, configuration);
        return services;
    }

    private static ServiceDescriptor Single(ServiceCollection services, Type serviceType) =>
        Assert.Single(services, d => d.ServiceType == serviceType);

    [Fact]
    public void TheNameIsIdentity() => Assert.Equal("Identity", new IdentityModule().Name);

    [Fact]
    public void TheApplicationAssemblyIsTheModulesApplicationAssembly() =>
        Assert.Same(typeof(SessionSettings).Assembly, new IdentityModule().ApplicationAssembly);

    [Fact]
    public void TheModuleApiIsRegisteredScoped()
    {
        ServiceDescriptor descriptor = Single(Registered(), typeof(IIdentityModuleApi));

        Assert.Equal(typeof(IdentityModuleApi), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void TheSeederIsRegisteredScoped()
    {
        ServiceDescriptor descriptor = Single(Registered(), typeof(IModuleSeeder));

        Assert.Equal(typeof(IdentitySeeder), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Theory]
    [InlineData(typeof(User), typeof(UserId))]
    [InlineData(typeof(RefreshTokenFamily), typeof(RefreshTokenFamilyId))]
    [InlineData(typeof(PortalAccount), typeof(PortalAccountId))]
    [InlineData(typeof(PortalSession), typeof(PortalSessionId))]
    public void EveryAggregateHasAScopedRepositoryOverTheModuleContext(Type aggregate, Type id)
    {
        ServiceDescriptor descriptor = Single(Registered(), typeof(IRepository<,>).MakeGenericType(aggregate, id));

        Assert.Equal(typeof(Repository<,,>).MakeGenericType(aggregate, id, typeof(IdentityDbContext)), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void TheUnitOfWorkAndTheContextAreRegisteredAndTheModuleIsRecorded()
    {
        ServiceCollection services = Registered();

        Assert.Contains(services, d => d.ServiceType == typeof(IdentityDbContext));
        Assert.Equal(ServiceLifetime.Scoped, Single(services, typeof(IIdentityUnitOfWork)).Lifetime);
        Assert.Equal(
            new ModuleUnitOfWork("Identity", typeof(SessionSettings).Assembly, typeof(IIdentityUnitOfWork)),
            Single(services, typeof(ModuleUnitOfWork)).ImplementationInstance);
        Assert.Equal(
            new ModuleDatabase("Identity", "identity", typeof(IdentityDbContext)),
            Single(services, typeof(ModuleDatabase)).ImplementationInstance);
    }

    [Theory]
    [InlineData(typeof(IPasswordHasher), typeof(AspNetPasswordHasher))]
    [InlineData(typeof(IRefreshTokenGenerator), typeof(RefreshTokenGenerator))]
    [InlineData(typeof(IAccessTokenIssuer), typeof(JwtAccessTokenIssuer))]
    public void TheSecurityAdaptersAreSingletons(Type service, Type implementation)
    {
        ServiceDescriptor descriptor = Single(Registered(), service);

        Assert.Equal(implementation, descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void TheSettingsAreSingletonsBoundFromTheirSections()
    {
        ServiceCollection services = Registered();

        ServiceDescriptor sessionsDescriptor = Single(services, typeof(SessionSettings));
        Assert.Equal(ServiceLifetime.Singleton, sessionsDescriptor.Lifetime);
        SessionSettings sessions = Assert.IsType<SessionSettings>(sessionsDescriptor.ImplementationInstance);
        Assert.Equal(TimeSpan.FromDays(14), sessions.RefreshTokenLifetime);
        Assert.Equal(5, sessions.MaxFailedSignInAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), sessions.LockoutWindow);

        JwtSettings jwt = Assert.IsType<JwtSettings>(Single(services, typeof(JwtSettings)).ImplementationInstance);
        Assert.Equal("crm", jwt.Issuer);
        Assert.Equal("crm-staff", jwt.Audience);
        Assert.Equal("crm-portal", jwt.PortalAudience);
        Assert.Equal(JwtSettingsFixture.SigningKey, jwt.SigningKey);
        Assert.Equal(TimeSpan.FromMinutes(15), jwt.AccessTokenLifetime);
        Assert.Equal(TimeSpan.FromSeconds(30), jwt.ClockSkew);
    }

    [Theory]
    [InlineData("en", "Preferred language must be en or ar.")]
    [InlineData("ar", "يجب أن تكون اللغة المفضلة en أو ar.")]
    public void TheLocalizationResourceResolvesTheLocaleMessageInBothLanguages(string culture, string expected)
    {
        LocalizationResource resource = Assert.IsType<LocalizationResource>(Single(Registered(), typeof(LocalizationResource)).ImplementationInstance);

        Assert.Equal(expected, resource.Manager.GetString(IdentityMessageKeys.Validation.LocaleUnsupported, CultureInfo.GetCultureInfo(culture)));
    }

    [Fact]
    public void TheStaffAndPortalBearerSchemesAreRegisteredWithDifferentAudiences()
    {
        ServiceCollection services = Registered();
        using ServiceProvider provider = services.BuildServiceProvider();

        var schemes = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value.Schemes.Select(s => s.Name).ToList();
        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();

        Assert.Contains(CrmAuthenticationSchemes.Staff, schemes);
        Assert.Contains(CrmAuthenticationSchemes.Portal, schemes);
        Assert.Equal("crm-staff", options.Get(CrmAuthenticationSchemes.Staff).TokenValidationParameters.ValidAudience);
        Assert.Equal("crm-portal", options.Get(CrmAuthenticationSchemes.Portal).TokenValidationParameters.ValidAudience);
        Assert.Equal(CrmAuthenticationSchemes.Staff, options.Get(CrmAuthenticationSchemes.Staff).TokenValidationParameters.AuthenticationType);
        Assert.Equal(CrmAuthenticationSchemes.Portal, options.Get(CrmAuthenticationSchemes.Portal).TokenValidationParameters.AuthenticationType);
        Assert.False(options.Get(CrmAuthenticationSchemes.Staff).MapInboundClaims);
        Assert.Equal(TimeSpan.FromSeconds(30), options.Get(CrmAuthenticationSchemes.Staff).TokenValidationParameters.ClockSkew);
    }

    [Fact]
    public void AMissingSessionsSectionFailsFast()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Registered(s =>
        {
            s.Remove("Identity:Sessions:RefreshTokenLifetime");
            s.Remove("Identity:Sessions:MaxFailedSignInAttempts");
            s.Remove("Identity:Sessions:LockoutWindow");
        }));

        Assert.Contains("Identity:Sessions", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ANonPositiveLockoutLimitFailsFastAtRegistration() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Registered(s => s["Identity:Sessions:MaxFailedSignInAttempts"] = "0"));

    [Fact]
    public void AZeroLockoutWindowFailsFastAtRegistration() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Registered(s => s["Identity:Sessions:LockoutWindow"] = "00:00:00"));

    [Fact]
    public void AMissingJwtSectionFailsFast()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Registered(s =>
        {
            foreach (string key in s.Keys.Where(k => k.StartsWith("Identity:Jwt:", StringComparison.Ordinal)).ToList())
            {
                s.Remove(key);
            }
        }));

        Assert.Contains("Identity:Jwt", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddServicesNeedsAConnectionString()
    {
        IConfiguration noConnection = new ConfigurationBuilder()
            .AddInMemoryCollection(JwtSettingsFixture.Configuration().Where(p => p.Key != "ConnectionStrings:Crm"))
            .Build();

        Assert.ThrowsAny<Exception>(() => new IdentityModule().AddServices(new ServiceCollection(), noConnection));
    }
}

public sealed class JwtSettingsValidationTests
{
    private static InvalidOperationException Refused(Action<Dictionary<string, string?>> tweak)
    {
        Dictionary<string, string?> settings = JwtSettingsFixture.Configuration();
        tweak(settings);
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        return Assert.Throws<InvalidOperationException>(() => new IdentityModule().AddServices(new ServiceCollection(), configuration));
    }

    [Theory]
    [InlineData("Issuer", "")]
    [InlineData("Audience", "")]
    [InlineData("PortalAudience", "")]
    [InlineData("PortalAudience", "crm-staff")]
    [InlineData("SigningKey", "")]
    [InlineData("SigningKey", "0123456789-0123456789-012345")]
    [InlineData("AccessTokenLifetime", "00:00:00")]
    [InlineData("AccessTokenLifetime", "-00:05:00")]
    [InlineData("ClockSkew", "-00:00:01")]
    public void AnInvalidSettingIsRefusedNamingTheSettingAndTheEnvironmentVariable(string name, string value)
    {
        InvalidOperationException exception = Refused(s => s[$"Identity:Jwt:{name}"] = value);

        Assert.Contains($"Identity:Jwt:{name}", exception.Message, StringComparison.Ordinal);
        Assert.Contains($"Identity__Jwt__{name}", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheExceptionNeverEchoesTheKeyValue()
    {
        InvalidOperationException exception = Refused(s => s["Identity:Jwt:SigningKey"] = "too-short-key-value");

        Assert.DoesNotContain("too-short-key-value", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ThirtyOneBytesAreRefusedAndThirtyTwoAreAccepted()
    {
        Refused(s => s["Identity:Jwt:SigningKey"] = new string('k', 31));

        IConfiguration ok = new ConfigurationBuilder()
            .AddInMemoryCollection(JwtSettingsFixture.Configuration(new string('k', 32)))
            .Build();
        new IdentityModule().AddServices(new ServiceCollection(), ok);
    }

    [Fact]
    public void TheKeyIsCountedInUtf8BytesNotCharacters()
    {
        // 16 Arabic letters are 32 UTF-8 bytes (accepted); 15 are 30 bytes (refused).
        IConfiguration ok = new ConfigurationBuilder()
            .AddInMemoryCollection(JwtSettingsFixture.Configuration(new string('ع', 16)))
            .Build();
        new IdentityModule().AddServices(new ServiceCollection(), ok);

        Refused(s => s["Identity:Jwt:SigningKey"] = new string('ع', 15));
    }

    [Fact]
    public void AZeroClockSkewIsAccepted()
    {
        Dictionary<string, string?> settings = JwtSettingsFixture.Configuration();
        settings["Identity:Jwt:ClockSkew"] = "00:00:00";
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        new IdentityModule().AddServices(new ServiceCollection(), configuration);
    }
}
