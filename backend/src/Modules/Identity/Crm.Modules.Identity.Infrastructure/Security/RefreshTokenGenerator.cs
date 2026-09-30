using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Crm.Modules.Identity.Application.Abstractions;

namespace Crm.Modules.Identity.Infrastructure.Security;

/// <summary>32 random bytes, base64url; only the SHA-256 hash is ever stored.</summary>
public sealed class RefreshTokenGenerator : IRefreshTokenGenerator
{
    public NewRefreshToken Create()
    {
        string token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        return new NewRefreshToken(token, Hash(token));
    }

    public string Hash(string refreshToken)
    {
        ArgumentNullException.ThrowIfNull(refreshToken);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
    }
}
