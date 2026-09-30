using Crm.Modules.Identity.Application.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace Crm.Modules.Identity.Infrastructure.Security;

/// <summary>PBKDF2 through the shared framework's PasswordHasher (ASP.NET Core Identity v3 format). No Identity
/// stores, UI, or EF package is referenced. SuccessRehashNeeded counts as success (no rehash-on-login in the MVP).</summary>
public sealed class AspNetPasswordHasher : IPasswordHasher
{
    private static readonly HashSubject Subject = new();
    private static readonly PasswordHasher<HashSubject> Hasher = new();

    // Verified for an unknown email, so it costs what a wrong password costs.
    private static readonly string DummyHash = Hasher.HashPassword(Subject, "dummy-password-for-timing-0");

    public string Hash(string password) => Hasher.HashPassword(Subject, password);

    public bool Verify(string passwordHash, string password) =>
        Hasher.VerifyHashedPassword(Subject, passwordHash, password)
            is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;

    public void VerifyAgainstDummy(string password) => Verify(DummyHash, password);

    private sealed class HashSubject;
}
