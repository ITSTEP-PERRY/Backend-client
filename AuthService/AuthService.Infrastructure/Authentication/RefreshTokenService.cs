using System.Security.Cryptography;
using System.Text;
using AuthService.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace AuthService.Infrastructure.Authentication;

public sealed class RefreshTokenService(IOptions<JwtOptions> options) : IRefreshTokenService
{
    private readonly JwtOptions _options = options.Value;
    public string GenerateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    public string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    public TimeSpan GetLifetime(bool rememberMe) => TimeSpan.FromDays(
        rememberMe ? _options.RememberMeRefreshTokenDays : _options.RefreshTokenDays);
    public bool IsRememberMeLifetime(TimeSpan lifetime) =>
        Math.Abs((lifetime - GetLifetime(true)).TotalSeconds) < Math.Abs((lifetime - GetLifetime(false)).TotalSeconds);
}
