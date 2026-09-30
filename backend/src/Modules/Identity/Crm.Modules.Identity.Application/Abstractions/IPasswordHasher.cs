namespace Crm.Modules.Identity.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string passwordHash, string password);

    /// <summary>Burns the same time as a real verification, so an unknown email costs the same as a wrong password.</summary>
    void VerifyAgainstDummy(string password);
}
