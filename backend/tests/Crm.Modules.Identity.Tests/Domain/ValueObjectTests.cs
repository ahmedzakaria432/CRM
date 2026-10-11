using Crm.Modules.Identity.Domain;
using Crm.Modules.Identity.Tests.Support;
using Crm.SharedKernel;

namespace Crm.Modules.Identity.Tests.Domain;

public sealed class DisplayNameTests
{
    [Fact]
    public void CompileProbeSeesTheTransitiveReferences()
    {
        Assert.NotNull(typeof(Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler));
        Assert.NotNull(typeof(Crm.Modules.Customers.Contracts.PortalContactMatch));
        Assert.NotNull(typeof(Crm.Modules.Organisation.Contracts.UserMemberships));
    }

    [Fact]
    public void CreateTrimsTheValue() => Assert.Equal("Omar Haddad", DisplayName.Create("  Omar Haddad \t").Value);

    [Fact]
    public void ArabicTextRoundTrips() => Assert.Equal("عمر حداد", DisplayName.Create(" عمر حداد ").Value);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void BlankIsRefused(string value)
    {
        var exception = Assert.Throws<DomainException>(() => DisplayName.Create(value));

        Assert.Equal("Identity.DisplayNameRequired", exception.Error.Code);
    }

    [Fact]
    public void TwoHundredCharactersAreAccepted() => Assert.Equal(200, DisplayName.Create(new string('a', 200)).Value.Length);

    [Fact]
    public void TwoHundredAndOneCharactersAreRefused()
    {
        var exception = Assert.Throws<DomainException>(() => DisplayName.Create(new string('a', 201)));

        Assert.Equal("Identity.DisplayNameTooLong", exception.Error.Code);
    }

    [Fact]
    public void TheLimitIsMeasuredAfterTrimming() => Assert.Equal(200, DisplayName.Create(" " + new string('a', 200) + " ").Value.Length);

    [Fact]
    public void NullIsAnArgumentError() => NullArguments.AssertThrowsArgumentNull(DisplayName.Create, new object?[] { null });

    [Fact]
    public void MaxLengthIsTwoHundred() => Assert.Equal(200, DisplayName.MaxLength);

    [Fact]
    public void EqualityIsByValue()
    {
        Assert.Equal(DisplayName.Create("A"), DisplayName.Create(" A "));
        Assert.Equal(DisplayName.Create("A").GetHashCode(), DisplayName.Create("A").GetHashCode());
        Assert.NotEqual(DisplayName.Create("A"), DisplayName.Create("B"));
    }
}

public sealed class EmailAddressTests
{
    [Fact]
    public void CreateTrimsAndLowerCases() => Assert.Equal("a@b.com", EmailAddress.Create(" A@B.COM ").Value);

    [Theory]
    [InlineData("a@b.c")]
    [InlineData("first.last@sub.example.co")]
    [InlineData("a+tag@example.com")]
    public void ValidShapesAreAccepted(string value) => Assert.Equal(value, EmailAddress.Create(value).Value);

    [Fact]
    public void TheLimitIs254Characters()
    {
        string ok = "a@" + new string('b', 254 - 2 - 4) + ".com";
        Assert.Equal(254, ok.Length);
        Assert.Equal(ok, EmailAddress.Create(ok).Value);

        string tooLong = "a@" + new string('b', 255 - 2 - 4) + ".com";
        Assert.Equal(255, tooLong.Length);
        Assert.Equal("Identity.EmailInvalid", Assert.Throws<DomainException>(() => EmailAddress.Create(tooLong)).Error.Code);
    }

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
    public void MalformedIsRefused(string value)
    {
        var exception = Assert.Throws<DomainException>(() => EmailAddress.Create(value));

        Assert.Equal("Identity.EmailInvalid", exception.Error.Code);
    }

    [Fact]
    public void NullIsAnArgumentError() => NullArguments.AssertThrowsArgumentNull(EmailAddress.Create, new object?[] { null });

    [Fact]
    public void TryCreateNeverThrows()
    {
        Assert.False(EmailAddress.TryCreate(null, out EmailAddress? none));
        Assert.Null(none);

        Assert.False(EmailAddress.TryCreate("nope", out EmailAddress? malformed));
        Assert.Null(malformed);

        Assert.True(EmailAddress.TryCreate(" A@B.COM ", out EmailAddress? valid));
        Assert.Equal("a@b.com", valid.Value);
    }

    [Fact]
    public void TryCreateRefusesAnOverlongAddress() => Assert.False(EmailAddress.TryCreate("a@" + new string('b', 260) + ".com", out _));

    [Fact]
    public void EqualityIsByTheNormalisedValue()
    {
        Assert.Equal(EmailAddress.Create("A@B.com"), EmailAddress.Create("a@b.COM"));
        Assert.NotEqual(EmailAddress.Create("a@b.com"), EmailAddress.Create("c@b.com"));
    }

    [Fact]
    public void MaxLengthIs254() => Assert.Equal(254, EmailAddress.MaxLength);
}

public sealed class PreferredLocaleTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("EN")]
    [InlineData(" en ")]
    public void EnglishVariantsReturnTheEnglishSingleton(string value) => Assert.Same(PreferredLocale.English, PreferredLocale.Create(value));

    [Theory]
    [InlineData("ar")]
    [InlineData("AR")]
    [InlineData(" ar ")]
    public void ArabicVariantsReturnTheArabicSingleton(string value) => Assert.Same(PreferredLocale.Arabic, PreferredLocale.Create(value));

    [Theory]
    [InlineData("fr")]
    [InlineData("")]
    [InlineData("en-US")]
    public void UnsupportedIsRefused(string value)
    {
        var exception = Assert.Throws<DomainException>(() => PreferredLocale.Create(value));

        Assert.Equal("Identity.LocaleUnsupported", exception.Error.Code);
    }

    [Fact]
    public void NullIsAnArgumentError() => NullArguments.AssertThrowsArgumentNull(PreferredLocale.Create, new object?[] { null });

    [Fact]
    public void TheValuesAreEnAndAr()
    {
        Assert.Equal("en", PreferredLocale.English.Value);
        Assert.Equal("ar", PreferredLocale.Arabic.Value);
    }

    [Fact]
    public void EqualityIsByValue()
    {
        Assert.Equal(PreferredLocale.English, PreferredLocale.Create("en"));
        Assert.NotEqual(PreferredLocale.English, PreferredLocale.Arabic);
    }
}

public sealed class LockoutPolicyTests
{
    [Fact]
    public void ValidConstructionStoresTheValues()
    {
        var policy = new LockoutPolicy(5, TimeSpan.FromMinutes(15));

        Assert.Equal(5, policy.MaxFailedAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), policy.Window);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveAttemptsAreRefused(int attempts) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new LockoutPolicy(attempts, TimeSpan.FromMinutes(1)));

    [Fact]
    public void ZeroWindowIsRefused() => Assert.Throws<ArgumentOutOfRangeException>(() => new LockoutPolicy(1, TimeSpan.Zero));

    [Fact]
    public void NegativeWindowIsRefused() => Assert.Throws<ArgumentOutOfRangeException>(() => new LockoutPolicy(1, TimeSpan.FromSeconds(-1)));

    [Fact]
    public void OneAttemptAndOneTickAreAccepted() => Assert.Equal(1, new LockoutPolicy(1, TimeSpan.FromTicks(1)).MaxFailedAttempts);

    [Fact]
    public void EqualityIsByValue()
    {
        Assert.Equal(new LockoutPolicy(3, TimeSpan.FromMinutes(1)), new LockoutPolicy(3, TimeSpan.FromMinutes(1)));
        Assert.NotEqual(new LockoutPolicy(3, TimeSpan.FromMinutes(1)), new LockoutPolicy(4, TimeSpan.FromMinutes(1)));
    }
}

public sealed class IdentityIdsTests
{
    [Fact]
    public void NewIdsAreNonEmptyAndDistinct()
    {
        Assert.NotEqual(Guid.Empty, UserId.New().Value);
        Assert.NotEqual(UserId.New(), UserId.New());
        Assert.NotEqual(PortalAccountId.New(), PortalAccountId.New());
        Assert.NotEqual(PortalSessionId.New(), PortalSessionId.New());
        Assert.NotEqual(RefreshTokenFamilyId.New(), RefreshTokenFamilyId.New());
        Assert.NotEqual(Guid.Empty, PortalAccountId.New().Value);
        Assert.NotEqual(Guid.Empty, PortalSessionId.New().Value);
        Assert.NotEqual(Guid.Empty, RefreshTokenFamilyId.New().Value);
    }

    [Fact]
    public void EqualityIsByValue()
    {
        Assert.Equal(new UserId(Ids.User), new UserId(Ids.User));
        Assert.Equal(new PortalAccountId(Ids.Account), new PortalAccountId(Ids.Account));
        Assert.Equal(new PortalSessionId(Ids.Session), new PortalSessionId(Ids.Session));
        Assert.Equal(new RefreshTokenFamilyId(Ids.Family), new RefreshTokenFamilyId(Ids.Family));
        Assert.NotEqual(new UserId(Ids.User), new UserId(Ids.OtherUser));
    }

    [Fact]
    public void DifferentIdTypesWithTheSameGuidAreNotEqual()
    {
        object user = new UserId(Ids.User);
        object account = new PortalAccountId(Ids.User);

        Assert.False(user.Equals(account));
    }
}
