using Crm.Modules.Identity.Domain;
using Crm.Modules.Identity.Tests.Support;
using Crm.SharedKernel;

namespace Crm.Modules.Identity.Tests.Domain;

public sealed class RefreshTokenFamilyTests
{
    private static readonly DateTimeOffset Now = Clock.Now;

    private static string CodeOf(Action action) => Assert.Throws<DomainException>(action).Error.Code;

    [Fact]
    public void StartCreatesOneTokenAndRaisesSessionStarted()
    {
        var userId = new UserId(Ids.User);
        var id = new RefreshTokenFamilyId(Ids.Family);

        RefreshTokenFamily family = RefreshTokenFamily.Start(userId, "h1", Now, Now.AddDays(14), id);

        Assert.Equal(id, family.Id);
        Assert.Equal(userId, family.UserId);
        Assert.False(family.IsRevoked);
        Assert.Null(family.RevokedAtUtc);
        Assert.Null(family.RevocationReason);
        RefreshToken token = Assert.Single(family.Tokens);
        Assert.Equal("h1", token.TokenHash);
        Assert.Equal(Now, token.IssuedAtUtc);
        Assert.Equal(Now.AddDays(14), token.ExpiresAtUtc);
        Assert.Null(token.RotatedAtUtc);
        SessionStarted started = Assert.IsType<SessionStarted>(Assert.Single(family.DomainEvents));
        Assert.Equal(userId, started.UserId);
        Assert.Equal(id, started.SessionId);
        Assert.Equal(Now, started.OccurredOnUtc);
    }

    [Fact]
    public void StartGeneratesAnIdWhenNoneIsSupplied() =>
        Assert.NotEqual(Guid.Empty, RefreshTokenFamily.Start(new UserId(Ids.User), "h", Now, Now.AddDays(1)).Id.Value);

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void StartRefusesABlankHash(string hash) =>
        Assert.Equal("Identity.RefreshTokenRequired", CodeOf(() => RefreshTokenFamily.Start(new UserId(Ids.User), hash, Now, Now.AddDays(1))));

    [Fact]
    public void StartRefusesAnExpiryAtOrBeforeNow()
    {
        Assert.Equal("Identity.RefreshTokenExpiryInvalid", CodeOf(() => RefreshTokenFamily.Start(new UserId(Ids.User), "h", Now, Now)));
        Assert.Equal("Identity.RefreshTokenExpiryInvalid", CodeOf(() => RefreshTokenFamily.Start(new UserId(Ids.User), "h", Now, Now.AddTicks(-1))));
        Assert.Single(RefreshTokenFamily.Start(new UserId(Ids.User), "h", Now, Now.AddTicks(1)).Tokens);
    }

    [Fact]
    public void StartGuardsANullUser() =>
        NullArguments.AssertThrowsArgumentNull(RefreshTokenFamily.Start, null, "h", Now, Now.AddDays(1), null);

    [Fact]
    public void RotateMarksThePresentedTokenAndAppendsTheNewOne()
    {
        RefreshTokenFamily family = Families.Start(hash: "h1");

        RefreshTokenRotation result = family.Rotate("h1", "h2", Now.AddMinutes(1), Now.AddDays(14));

        Assert.Equal(RefreshTokenRotation.Rotated, result);
        Assert.Equal(2, family.Tokens.Count);
        RefreshToken old = family.Tokens.Single(t => t.TokenHash == "h1");
        RefreshToken fresh = family.Tokens.Single(t => t.TokenHash == "h2");
        Assert.Equal(Now.AddMinutes(1), old.RotatedAtUtc);
        Assert.Null(fresh.RotatedAtUtc);
        Assert.Equal(Now.AddMinutes(1), fresh.IssuedAtUtc);
        Assert.Equal(Now.AddDays(14), fresh.ExpiresAtUtc);
        Assert.False(family.IsRevoked);
    }

    [Fact]
    public void ReusingARotatedTokenRevokesTheFamilyAndAddsNothing()
    {
        RefreshTokenFamily family = Families.Start(hash: "h1");
        family.Rotate("h1", "h2", Now, Now.AddDays(14));

        RefreshTokenRotation result = family.Rotate("h1", "h3", Now.AddMinutes(5), Now.AddDays(14));

        Assert.Equal(RefreshTokenRotation.ReuseDetected, result);
        Assert.True(family.IsRevoked);
        Assert.Equal(SessionRevocationReason.ReuseDetected, family.RevocationReason);
        Assert.Equal(Now.AddMinutes(5), family.RevokedAtUtc);
        Assert.Equal(2, family.Tokens.Count);
    }

    [Fact]
    public void ARevokedFamilyAnswersRevokedAndChangesNothing()
    {
        RefreshTokenFamily family = Families.Start(hash: "h1");
        family.Rotate("h1", "h2", Now, Now.AddDays(14));
        family.Revoke(SessionRevocationReason.SignedOut, Now);

        Assert.Equal(RefreshTokenRotation.Revoked, family.Rotate("h2", "h3", Now, Now.AddDays(14)));
        Assert.Equal(RefreshTokenRotation.Revoked, family.Rotate("h1", "h3", Now, Now.AddDays(14)));
        Assert.Equal(2, family.Tokens.Count);
        Assert.Equal(SessionRevocationReason.SignedOut, family.RevocationReason);
    }

    [Fact]
    public void AnUnknownHashAnswersRevoked()
    {
        RefreshTokenFamily family = Families.Start(hash: "h1");

        Assert.Equal(RefreshTokenRotation.Revoked, family.Rotate("zzz", "h2", Now, Now.AddDays(14)));
        Assert.Single(family.Tokens);
        Assert.False(family.IsRevoked);
    }

    [Fact]
    public void TheHashComparisonIsOrdinal()
    {
        RefreshTokenFamily family = Families.Start(hash: "AbC");

        Assert.Equal(RefreshTokenRotation.Revoked, family.Rotate("abc", "h2", Now, Now.AddDays(14)));
    }

    [Fact]
    public void ATokenExpiringExactlyNowIsExpiredAndOneTickLaterIsValid()
    {
        RefreshTokenFamily expiring = Families.Start(hash: "h1", expires: Now.AddMinutes(1));

        Assert.Equal(RefreshTokenRotation.Expired, expiring.Rotate("h1", "h2", Now.AddMinutes(1), Now.AddDays(1)));
        Assert.Single(expiring.Tokens);
        Assert.Null(expiring.Tokens.Single().RotatedAtUtc);
        Assert.False(expiring.IsRevoked);

        RefreshTokenFamily valid = Families.Start(hash: "h1", expires: Now.AddMinutes(1).AddTicks(1));
        Assert.Equal(RefreshTokenRotation.Rotated, valid.Rotate("h1", "h2", Now.AddMinutes(1), Now.AddDays(1)));
    }

    [Fact]
    public void ExpiryIsCheckedBeforeTheNewHashArgument()
    {
        RefreshTokenFamily family = Families.Start(hash: "h1", expires: Now.AddMinutes(1));

        Assert.Equal(RefreshTokenRotation.Expired, family.Rotate("h1", "", Now.AddMinutes(2), Now.AddMinutes(2)));
    }

    [Theory]
    [InlineData("", 60)]
    [InlineData("  ", 60)]
    [InlineData("h2", 0)]
    [InlineData("h2", -60)]
    public void AValidTokenWithABadNewHashOrExpiryThrowsAndLeavesTheOldTokenUnrotated(string newHash, int newExpirySeconds)
    {
        RefreshTokenFamily family = Families.Start(hash: "h1");

        Assert.Equal("Identity.RefreshTokenRequired", CodeOf(() => family.Rotate("h1", newHash, Now, Now.AddSeconds(newExpirySeconds))));

        Assert.Null(family.Tokens.Single().RotatedAtUtc);
        Assert.Single(family.Tokens);
    }

    [Fact]
    public void AChainOfThreeRotationsLeavesExactlyOneLiveToken()
    {
        RefreshTokenFamily family = Families.Start(hash: "h1");

        Assert.Equal(RefreshTokenRotation.Rotated, family.Rotate("h1", "h2", Now, Now.AddDays(14)));
        Assert.Equal(RefreshTokenRotation.Rotated, family.Rotate("h2", "h3", Now, Now.AddDays(14)));
        Assert.Equal(RefreshTokenRotation.Rotated, family.Rotate("h3", "h4", Now, Now.AddDays(14)));

        Assert.Equal(4, family.Tokens.Count);
        Assert.Equal("h4", Assert.Single(family.Tokens, t => t.RotatedAtUtc is null).TokenHash);
    }

    [Fact]
    public void RevokeSetsTimeAndReason()
    {
        RefreshTokenFamily family = Families.Start();

        family.Revoke(SessionRevocationReason.UserDeactivated, Now.AddHours(1));

        Assert.True(family.IsRevoked);
        Assert.Equal(Now.AddHours(1), family.RevokedAtUtc);
        Assert.Equal(SessionRevocationReason.UserDeactivated, family.RevocationReason);
    }

    [Fact]
    public void RevokeIsIdempotentAndKeepsTheFirstReasonAndTime()
    {
        RefreshTokenFamily family = Families.Start();
        family.Revoke(SessionRevocationReason.SignedOut, Now);

        family.Revoke(SessionRevocationReason.PasswordReset, Now.AddHours(1));

        Assert.Equal(SessionRevocationReason.SignedOut, family.RevocationReason);
        Assert.Equal(Now, family.RevokedAtUtc);
    }

    [Theory]
    [InlineData(SessionRevocationReason.SignedOut)]
    [InlineData(SessionRevocationReason.ReuseDetected)]
    [InlineData(SessionRevocationReason.UserDeactivated)]
    [InlineData(SessionRevocationReason.PasswordChanged)]
    [InlineData(SessionRevocationReason.PasswordReset)]
    [InlineData(SessionRevocationReason.PortalAccessRevoked)]
    public void EveryReasonIsAccepted(SessionRevocationReason reason)
    {
        RefreshTokenFamily family = Families.Start();

        family.Revoke(reason, Now);

        Assert.Equal(reason, family.RevocationReason);
    }

    [Fact]
    public void TokensIsReadOnly()
    {
        RefreshTokenFamily family = Families.Start();

        Assert.False(family.Tokens is List<RefreshToken>);
        Assert.Throws<NotSupportedException>(() => ((IList<RefreshToken>)family.Tokens).Clear());
    }
}

public sealed class PortalSessionTests
{
    private static readonly DateTimeOffset Now = Clock.Now;

    private static string CodeOf(Action action) => Assert.Throws<DomainException>(action).Error.Code;

    [Fact]
    public void StartCreatesOneTokenAndRaisesPortalSessionStarted()
    {
        var accountId = new PortalAccountId(Ids.Account);
        var id = new PortalSessionId(Ids.Session);

        PortalSession session = PortalSession.Start(accountId, "h1", Now, Now.AddDays(14), id);

        Assert.Equal(id, session.Id);
        Assert.Equal(accountId, session.PortalAccountId);
        Assert.False(session.IsRevoked);
        RefreshToken token = Assert.Single(session.Tokens);
        Assert.Equal("h1", token.TokenHash);
        Assert.Equal(Now, token.IssuedAtUtc);
        Assert.Equal(Now.AddDays(14), token.ExpiresAtUtc);
        Assert.Null(token.RotatedAtUtc);
        PortalSessionStarted started = Assert.IsType<PortalSessionStarted>(Assert.Single(session.DomainEvents));
        Assert.Equal(accountId, started.AccountId);
        Assert.Equal(id, started.SessionId);
        Assert.Equal(Now, started.OccurredOnUtc);
    }

    [Fact]
    public void StartGeneratesAnIdWhenNoneIsSupplied() =>
        Assert.NotEqual(Guid.Empty, PortalSession.Start(new PortalAccountId(Ids.Account), "h", Now, Now.AddDays(1)).Id.Value);

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void StartRefusesABlankHash(string hash) =>
        Assert.Equal("Identity.RefreshTokenRequired", CodeOf(() => PortalSession.Start(new PortalAccountId(Ids.Account), hash, Now, Now.AddDays(1))));

    [Fact]
    public void StartRefusesAnExpiryAtOrBeforeNow()
    {
        Assert.Equal("Identity.RefreshTokenExpiryInvalid", CodeOf(() => PortalSession.Start(new PortalAccountId(Ids.Account), "h", Now, Now)));
        Assert.Equal("Identity.RefreshTokenExpiryInvalid", CodeOf(() => PortalSession.Start(new PortalAccountId(Ids.Account), "h", Now, Now.AddTicks(-1))));
    }

    [Fact]
    public void StartGuardsANullAccount() =>
        NullArguments.AssertThrowsArgumentNull(PortalSession.Start, null, "h", Now, Now.AddDays(1), null);

    [Fact]
    public void RotateMarksThePresentedTokenAndAppendsTheNewOne()
    {
        PortalSession session = Sessions.Start(hash: "h1");

        RefreshTokenRotation result = session.Rotate("h1", "h2", Now.AddMinutes(1), Now.AddDays(14));

        Assert.Equal(RefreshTokenRotation.Rotated, result);
        Assert.Equal(2, session.Tokens.Count);
        Assert.Equal(Now.AddMinutes(1), session.Tokens.Single(t => t.TokenHash == "h1").RotatedAtUtc);
        Assert.Null(session.Tokens.Single(t => t.TokenHash == "h2").RotatedAtUtc);
    }

    [Fact]
    public void ReusingARotatedTokenRevokesTheSession()
    {
        PortalSession session = Sessions.Start(hash: "h1");
        session.Rotate("h1", "h2", Now, Now.AddDays(14));

        RefreshTokenRotation result = session.Rotate("h1", "h3", Now.AddMinutes(5), Now.AddDays(14));

        Assert.Equal(RefreshTokenRotation.ReuseDetected, result);
        Assert.Equal(SessionRevocationReason.ReuseDetected, session.RevocationReason);
        Assert.Equal(Now.AddMinutes(5), session.RevokedAtUtc);
        Assert.Equal(2, session.Tokens.Count);
    }

    [Fact]
    public void ARevokedSessionAnswersRevokedAndChangesNothing()
    {
        PortalSession session = Sessions.Start(hash: "h1");
        session.Revoke(SessionRevocationReason.SignedOut, Now);

        Assert.Equal(RefreshTokenRotation.Revoked, session.Rotate("h1", "h2", Now, Now.AddDays(14)));
        Assert.Single(session.Tokens);
    }

    [Fact]
    public void AnUnknownHashAnswersRevoked() =>
        Assert.Equal(RefreshTokenRotation.Revoked, Sessions.Start(hash: "h1").Rotate("zzz", "h2", Now, Now.AddDays(14)));

    [Fact]
    public void TheHashComparisonIsOrdinal() =>
        Assert.Equal(RefreshTokenRotation.Revoked, Sessions.Start(hash: "AbC").Rotate("abc", "h2", Now, Now.AddDays(14)));

    [Fact]
    public void ATokenExpiringExactlyNowIsExpiredAndOneTickLaterIsValid()
    {
        PortalSession expiring = Sessions.Start(hash: "h1", expires: Now.AddMinutes(1));

        Assert.Equal(RefreshTokenRotation.Expired, expiring.Rotate("h1", "h2", Now.AddMinutes(1), Now.AddDays(1)));
        Assert.Single(expiring.Tokens);
        Assert.False(expiring.IsRevoked);

        PortalSession valid = Sessions.Start(hash: "h1", expires: Now.AddMinutes(1).AddTicks(1));
        Assert.Equal(RefreshTokenRotation.Rotated, valid.Rotate("h1", "h2", Now.AddMinutes(1), Now.AddDays(1)));
    }

    [Fact]
    public void ExpiryIsCheckedBeforeTheNewHashArgument() =>
        Assert.Equal(RefreshTokenRotation.Expired, Sessions.Start(hash: "h1", expires: Now.AddMinutes(1)).Rotate("h1", "", Now.AddMinutes(2), Now.AddMinutes(2)));

    [Theory]
    [InlineData("", 60)]
    [InlineData("h2", 0)]
    [InlineData("h2", -60)]
    public void AValidTokenWithABadNewHashOrExpiryThrowsAndLeavesTheOldTokenUnrotated(string newHash, int newExpirySeconds)
    {
        PortalSession session = Sessions.Start(hash: "h1");

        Assert.Equal("Identity.RefreshTokenRequired", CodeOf(() => session.Rotate("h1", newHash, Now, Now.AddSeconds(newExpirySeconds))));

        Assert.Null(session.Tokens.Single().RotatedAtUtc);
    }

    [Fact]
    public void AChainOfThreeRotationsLeavesExactlyOneLiveToken()
    {
        PortalSession session = Sessions.Start(hash: "h1");

        session.Rotate("h1", "h2", Now, Now.AddDays(14));
        session.Rotate("h2", "h3", Now, Now.AddDays(14));
        session.Rotate("h3", "h4", Now, Now.AddDays(14));

        Assert.Equal("h4", Assert.Single(session.Tokens, t => t.RotatedAtUtc is null).TokenHash);
    }

    [Fact]
    public void RevokeIsIdempotentAndKeepsTheFirstReasonAndTime()
    {
        PortalSession session = Sessions.Start();
        session.Revoke(SessionRevocationReason.PortalAccessRevoked, Now);

        session.Revoke(SessionRevocationReason.SignedOut, Now.AddHours(1));

        Assert.True(session.IsRevoked);
        Assert.Equal(SessionRevocationReason.PortalAccessRevoked, session.RevocationReason);
        Assert.Equal(Now, session.RevokedAtUtc);
    }

    [Theory]
    [InlineData(SessionRevocationReason.SignedOut)]
    [InlineData(SessionRevocationReason.ReuseDetected)]
    [InlineData(SessionRevocationReason.UserDeactivated)]
    [InlineData(SessionRevocationReason.PasswordChanged)]
    [InlineData(SessionRevocationReason.PasswordReset)]
    [InlineData(SessionRevocationReason.PortalAccessRevoked)]
    public void EveryReasonIsAccepted(SessionRevocationReason reason)
    {
        PortalSession session = Sessions.Start();

        session.Revoke(reason, Now);

        Assert.Equal(reason, session.RevocationReason);
    }

    [Fact]
    public void TokensIsReadOnly() => Assert.Throws<NotSupportedException>(() => ((IList<RefreshToken>)Sessions.Start().Tokens).Clear());
}

public sealed class DomainEventRecordsTests
{
    private static readonly DateTimeOffset Now = Clock.Now;

    [Fact]
    public void TheRecordsCarryTheirArgumentsAndAreDomainEvents()
    {
        var userId = new UserId(Ids.User);
        var accountId = new PortalAccountId(Ids.Account);

        IDomainEvent[] events =
        [
            new UserDeactivated(userId, Now),
            new UserPasswordChanged(userId, Ids.Session, Now),
            new SessionStarted(userId, new RefreshTokenFamilyId(Ids.Family), Now),
            new PortalPasswordChanged(accountId, null, Now),
            new PortalSessionStarted(accountId, new PortalSessionId(Ids.Session), Now),
        ];

        Assert.All(events, e => Assert.Equal(Now, e.OccurredOnUtc));
        Assert.Equal(userId, ((UserDeactivated)events[0]).UserId);
        Assert.Equal(Ids.Session, ((UserPasswordChanged)events[1]).KeepSessionId);
        Assert.Equal(Ids.Family, ((SessionStarted)events[2]).SessionId.Value);
        Assert.Null(((PortalPasswordChanged)events[3]).KeepSessionId);
        Assert.Equal(Ids.Session, ((PortalSessionStarted)events[4]).SessionId.Value);
    }

    [Fact]
    public void EqualityIsByValue()
    {
        var userId = new UserId(Ids.User);

        Assert.Equal(new UserDeactivated(userId, Now), new UserDeactivated(new UserId(Ids.User), Now));
        Assert.NotEqual(new UserDeactivated(userId, Now), new UserDeactivated(userId, Now.AddTicks(1)));
        Assert.Equal(new UserPasswordChanged(userId, null, Now), new UserPasswordChanged(userId, null, Now));
        Assert.NotEqual(new UserPasswordChanged(userId, null, Now), new UserPasswordChanged(userId, Ids.Session, Now));
    }
}

public sealed class EnumContractTests
{
    [Fact]
    public void RefreshTokenRotationNamesAndOrder() =>
        Assert.Equal(["Rotated", "ReuseDetected", "Expired", "Revoked"], Enum.GetNames<RefreshTokenRotation>());

    [Fact]
    public void SessionRevocationReasonNamesAndOrderAreTheStorageContract() =>
        Assert.Equal(
            ["SignedOut", "ReuseDetected", "UserDeactivated", "PasswordChanged", "PasswordReset", "PortalAccessRevoked"],
            Enum.GetNames<SessionRevocationReason>());
}
