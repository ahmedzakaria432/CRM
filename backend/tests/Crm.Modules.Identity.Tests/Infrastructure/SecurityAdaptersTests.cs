using System.Text.RegularExpressions;
using Crm.BuildingBlocks.Application.Abstractions;
using Crm.Modules.Identity.Application.Abstractions;
using Crm.Modules.Identity.Infrastructure.Security;
using Crm.Modules.Identity.Tests.Support;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Crm.Modules.Identity.Tests.Infrastructure;

public sealed class AspNetPasswordHasherTests
{
    private readonly AspNetPasswordHasher _hasher = new();

    [Fact]
    public void HashReturnsANonEmptyStringDifferentFromThePassword()
    {
        string hash = _hasher.Hash("Correct-horse-9");

        Assert.NotEmpty(hash);
        Assert.NotEqual("Correct-horse-9", hash);
    }

    [Fact]
    public void TwoHashesOfTheSamePasswordDifferBecauseTheyAreSalted() =>
        Assert.NotEqual(_hasher.Hash("Correct-horse-9"), _hasher.Hash("Correct-horse-9"));

    [Fact]
    public void VerifyAcceptsTheRightPasswordAndRefusesWrongOnes()
    {
        string hash = _hasher.Hash("Correct-horse-9");

        Assert.True(_hasher.Verify(hash, "Correct-horse-9"));
        Assert.False(_hasher.Verify(hash, "Correct-horse-8"));
        Assert.False(_hasher.Verify(hash, string.Empty));
        Assert.False(_hasher.Verify(hash, "correct-horse-9"));
    }

    [Fact]
    public void AnArabicPasswordRoundTrips()
    {
        string hash = _hasher.Hash("كلمة-سر-قوية-123");

        Assert.True(_hasher.Verify(hash, "كلمة-سر-قوية-123"));
        Assert.False(_hasher.Verify(hash, "كلمة-سر-قوية-124"));
    }

    [Fact]
    public void ATamperedHashIsRefusedWithoutThrowing()
    {
        string hash = _hasher.Hash("Correct-horse-9");
        string tampered = hash[..^4] + (hash.EndsWith("AAAA", StringComparison.Ordinal) ? "BBBB" : "AAAA");

        Assert.False(_hasher.Verify(tampered, "Correct-horse-9"));
    }

    [Fact]
    public void ABase64StringThatIsNotAHashIsRefusedWithoutThrowing()
    {
        Assert.False(_hasher.Verify("AAAA", "Correct-horse-9"));
        Assert.False(_hasher.Verify(string.Empty, "Correct-horse-9"));
    }

    [Fact]
    public void AStoredValueThatIsNotBase64ThrowsAFormatExceptionRatherThanFailing()
    {
        Assert.Throws<FormatException>(() => _hasher.Verify("not-a-hash", "Correct-horse-9"));
    }

    [Theory]
    [InlineData("Correct-horse-9")]
    [InlineData("")]
    public void VerifyAgainstDummyReturnsWithoutThrowing(string password) =>
        _hasher.VerifyAgainstDummy(password);
}

public sealed partial class RefreshTokenGeneratorTests
{
    private readonly RefreshTokenGenerator _generator = new();

    [GeneratedRegex("^[A-Za-z0-9_-]{43}$")]
    private static partial Regex Base64UrlOf32Bytes();

    [Fact]
    public void ATokenIsBase64UrlOf32Bytes()
    {
        NewRefreshToken created = _generator.Create();

        Assert.Matches(Base64UrlOf32Bytes(), created.Token);
        Assert.Equal(32, Convert.FromBase64String(created.Token.Replace('-', '+').Replace('_', '/') + "=").Length);
    }

    [Fact]
    public void TheTokenHashIsTheHashOfTheToken()
    {
        NewRefreshToken created = _generator.Create();

        Assert.Equal(_generator.Hash(created.Token), created.TokenHash);
    }

    [Fact]
    public void TwoTokensDiffer()
    {
        Assert.NotEqual(_generator.Create().Token, _generator.Create().Token);
        Assert.NotEqual(_generator.Create().TokenHash, _generator.Create().TokenHash);
    }

    [Fact]
    public void HashIsUpperCaseHexSha256WithAKnownVector() =>
        Assert.Equal("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", _generator.Hash("abc"));

    [Fact]
    public void HashIsDeterministicAndDiffersPerInput()
    {
        Assert.Equal(_generator.Hash("x"), _generator.Hash("x"));
        Assert.NotEqual(_generator.Hash("x"), _generator.Hash("y"));
        Assert.Equal(64, _generator.Hash("x").Length);
    }

    [Fact]
    public void HashOfNullIsAnArgumentError() => NullArguments.AssertThrowsArgumentNull(_generator.Hash, new object?[] { null });
}

public sealed class JwtAccessTokenIssuerTests
{
    private static readonly DateTimeOffset Now = Clock.Now;

    private readonly JwtAccessTokenIssuer _issuer = new(JwtSettingsFixture.Valid());

    private static AccessTokenSubject Subject(params TokenMembership[] memberships) =>
        new(Ids.User, "عمر حداد", Ids.TenantA.Value, Ids.Session, memberships);

    private static PortalAccessTokenSubject PortalSubject(bool admin = true) =>
        new(Ids.Account, "Layla Saleh", Ids.TenantA.Value, Ids.Session, Ids.Customer, Ids.Contact, admin, Ids.BranchA1.Value, Ids.DeptA1.Value);

    [Fact]
    public void TheStaffTokenHeaderIsHs256()
    {
        JsonWebToken token = Tokens.Read(_issuer.Issue(Subject(), Now).Token);

        Assert.Equal("HS256", token.Alg);
    }

    [Fact]
    public void TheStaffTokenCarriesIssuerAndTheStaffAudience()
    {
        JsonWebToken token = Tokens.Read(_issuer.Issue(Subject(), Now).Token);

        Assert.Equal("crm", token.Issuer);
        Assert.Equal(["crm-staff"], token.Audiences);
    }

    [Fact]
    public void TheStaffTokenCarriesTheIdentityClaims()
    {
        JsonWebToken token = Tokens.Read(_issuer.Issue(Subject(), Now).Token);

        Assert.Equal(Ids.User.ToString("D"), token.GetPayloadValue<string>(CrmClaimTypes.UserId));
        Assert.Equal("عمر حداد", token.GetPayloadValue<string>(CrmClaimTypes.DisplayName));
        Assert.Equal(Ids.TenantA.Value.ToString("D"), token.GetPayloadValue<string>(CrmClaimTypes.TenantId));
        Assert.Equal(Ids.Session.ToString("D"), token.GetPayloadValue<string>(CrmClaimTypes.SessionId));
    }

    [Fact]
    public void ThereIsOneMembershipClaimPerMembership()
    {
        var first = new TokenMembership(Ids.DeptA1.Value, Ids.BranchA1.Value, "Agent");
        var second = new TokenMembership(Ids.DeptA2.Value, Ids.BranchA2.Value, "Supervisor");

        JsonWebToken token = Tokens.Read(_issuer.Issue(Subject(first, second), Now).Token);

        string[] memberships = token.GetPayloadValue<string[]>(CrmClaimTypes.Membership);
        Assert.Equal(
            [$"{Ids.DeptA1.Value:D}|{Ids.BranchA1.Value:D}|Agent", $"{Ids.DeptA2.Value:D}|{Ids.BranchA2.Value:D}|Supervisor"],
            memberships);
    }

    [Fact]
    public void TheRoleClaimIsDistinctPerRole()
    {
        var one = new TokenMembership(Ids.DeptA1.Value, Ids.BranchA1.Value, "Agent");
        var two = new TokenMembership(Ids.DeptA2.Value, Ids.BranchA2.Value, "Agent");
        var three = new TokenMembership(Ids.DeptA2.Value, Ids.BranchA2.Value, "Supervisor");

        JsonWebToken token = Tokens.Read(_issuer.Issue(Subject(one, two, three), Now).Token);

        Assert.Equal(["Agent", "Supervisor"], token.GetPayloadValue<string[]>(CrmClaimTypes.Role));
    }

    [Fact]
    public void NoMembershipsMeansNoMembershipOrRoleValues()
    {
        JsonWebToken token = Tokens.Read(_issuer.Issue(Subject(), Now).Token);

        Assert.Empty(token.GetPayloadValue<string[]>(CrmClaimTypes.Membership));
        Assert.Empty(token.GetPayloadValue<string[]>(CrmClaimTypes.Role));
    }

    [Fact]
    public void TheJtiIsPresentAndUniquePerCall()
    {
        string first = Tokens.Read(_issuer.Issue(Subject(), Now).Token).Id;
        string second = Tokens.Read(_issuer.Issue(Subject(), Now).Token).Id;

        Assert.NotEmpty(first);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void TheTimeClaimsAreUnixSecondsOfTheFixedNow()
    {
        IssuedAccessToken issued = _issuer.Issue(Subject(), Now);
        JsonWebToken token = Tokens.Read(issued.Token);

        Assert.Equal(Now.ToUnixTimeSeconds(), token.GetPayloadValue<long>("iat"));
        Assert.Equal(Now.ToUnixTimeSeconds(), token.GetPayloadValue<long>("nbf"));
        Assert.Equal((Now + TimeSpan.FromMinutes(15)).ToUnixTimeSeconds(), token.GetPayloadValue<long>("exp"));
        Assert.Equal(Now + TimeSpan.FromMinutes(15), issued.ExpiresAtUtc);
    }

    [Fact]
    public async Task TheStaffTokenValidatesWithTheSameKeyAndAudience()
    {
        TokenValidationResult result = await Tokens.ValidateAsync(_issuer.Issue(Subject(), Now).Token, "crm-staff");

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task TheStaffTokenDoesNotValidateWithADifferentKey()
    {
        TokenValidationResult result = await Tokens.ValidateAsync(
            _issuer.Issue(Subject(), Now).Token, "crm-staff", "another-signing-key-another-signing-key");

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task TheStaffTokenDoesNotValidateAgainstThePortalAudience()
    {
        TokenValidationResult result = await Tokens.ValidateAsync(_issuer.Issue(Subject(), Now).Token, "crm-portal");

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ATokenWithTheAlgorithmReplacedByNoneDoesNotValidate()
    {
        string[] parts = _issuer.Issue(Subject(), Now).Token.Split('.');
        string header = Base64UrlEncoder.Encode("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        string unsigned = $"{header}.{parts[1]}.";

        TokenValidationResult result = await Tokens.ValidateAsync(unsigned, "crm-staff");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ThePortalTokenUsesThePortalAudience()
    {
        JsonWebToken token = Tokens.Read(_issuer.IssuePortal(PortalSubject(), Now).Token);

        Assert.Equal(["crm-portal"], token.Audiences);
        Assert.Equal("HS256", token.Alg);
    }

    [Fact]
    public void ThePortalTokenCarriesTheGrantClaims()
    {
        JsonWebToken token = Tokens.Read(_issuer.IssuePortal(PortalSubject(), Now).Token);

        Assert.Equal(Ids.Account.ToString("D"), token.GetPayloadValue<string>(CrmClaimTypes.UserId));
        Assert.Equal(Ids.Session.ToString("D"), token.GetPayloadValue<string>(CrmClaimTypes.SessionId));
        Assert.Equal(Ids.Customer.ToString("D"), token.GetPayloadValue<string>(CrmClaimTypes.PortalCustomerId));
        Assert.Equal(Ids.Contact.ToString("D"), token.GetPayloadValue<string>(CrmClaimTypes.PortalContactId));
        Assert.Equal(Ids.BranchA1.Value.ToString("D"), token.GetPayloadValue<string>(CrmClaimTypes.PortalBranchId));
        Assert.Equal(Ids.DeptA1.Value.ToString("D"), token.GetPayloadValue<string>(CrmClaimTypes.PortalDepartmentId));
        Assert.Equal("true", token.GetPayloadValue<string>(CrmClaimTypes.PortalAccountAdmin));
    }

    [Fact]
    public void ThePortalAdminClaimIsTheStringFalseForAPlainContact()
    {
        JsonWebToken token = Tokens.Read(_issuer.IssuePortal(PortalSubject(admin: false), Now).Token);

        Assert.Equal("false", token.GetPayloadValue<string>(CrmClaimTypes.PortalAccountAdmin));
    }

    [Fact]
    public void ThePortalTokenHasNoMembershipAndNoRoleClaims()
    {
        JsonWebToken token = Tokens.Read(_issuer.IssuePortal(PortalSubject(), Now).Token);

        Assert.False(token.TryGetPayloadValue<object>(CrmClaimTypes.Membership, out _));
        Assert.False(token.TryGetPayloadValue<object>(CrmClaimTypes.Role, out _));
    }

    [Fact]
    public void ThePortalTokenTimeClaimsAreUnixSecondsOfTheFixedNow()
    {
        IssuedAccessToken issued = _issuer.IssuePortal(PortalSubject(), Now);
        JsonWebToken token = Tokens.Read(issued.Token);

        Assert.Equal((Now + TimeSpan.FromMinutes(15)).ToUnixTimeSeconds(), token.GetPayloadValue<long>("exp"));
        Assert.Equal(Now + TimeSpan.FromMinutes(15), issued.ExpiresAtUtc);
        Assert.NotEmpty(token.Id);
    }

    [Fact]
    public async Task TheTwoAudiencesAreNotInterchangeable()
    {
        string portal = _issuer.IssuePortal(PortalSubject(), Now).Token;

        Assert.True((await Tokens.ValidateAsync(portal, "crm-portal")).IsValid);
        Assert.False((await Tokens.ValidateAsync(portal, "crm-staff")).IsValid);
    }

    [Fact]
    public void NullSubjectsAreArgumentErrors()
    {
        NullArguments.AssertThrowsArgumentNull(_issuer.Issue, null, Now);
        NullArguments.AssertThrowsArgumentNull(_issuer.IssuePortal, null, Now);
    }
}
