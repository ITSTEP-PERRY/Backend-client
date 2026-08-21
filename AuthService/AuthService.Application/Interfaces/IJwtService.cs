using AuthService.Domain.Entities;

namespace AuthService.Application.Interfaces;

public interface IJwtService
{
    string GenerateAccessToken(User user);

    int GetAccessTokenExpirationSeconds();
}