using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AuthService.Application.DTOs.Internal;
using AuthService.Application.Exceptions;
using AuthService.Application.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.Infrastructure.Authentication;

public sealed class ServiceTokenService(IOptions<InternalJwtOptions> options) : IServiceTokenService
{
    private readonly InternalJwtOptions _options = options.Value;

    public ServiceTokenResponse Issue(string serviceName, string credential)
    {
        var service = _options.Services.FirstOrDefault(x =>
            string.Equals(x.Name, serviceName.Trim(), StringComparison.Ordinal));
        if (service is null || !CredentialMatches(credential, service.CredentialHash))
            throw new AuthException("INVALID_SERVICE_CREDENTIALS", "Invalid service credentials.", 401);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, service.Name),
            new(InternalAuthConstants.TokenUseClaim, InternalAuthConstants.TokenUseService),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(service.Permissions.Distinct(StringComparer.Ordinal)
            .Select(permission => new Claim(InternalAuthConstants.PermissionClaim, permission)));

        var expiresIn = checked(_options.AccessTokenMinutes * 60);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningSecret)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            _options.Issuer, _options.Audience, claims,
            expires: DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes),
            signingCredentials: credentials);
        return new ServiceTokenResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresIn = expiresIn
        };
    }

    private static bool CredentialMatches(string credential, string configuredHash)
    {
        byte[] expected;
        try { expected = Convert.FromHexString(configuredHash); }
        catch (FormatException) { return false; }
        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(credential));
        return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
