namespace Crm.Modules.Identity.Application.Abstractions;

public interface IRefreshTokenGenerator
{
    NewRefreshToken Create();

    /// <summary>The stored form of a presented token: its SHA-256 hash.</summary>
    string Hash(string refreshToken);
}

/// <summary>Token is handed to the client once; only TokenHash is stored.</summary>
public sealed record NewRefreshToken(string Token, string TokenHash);
