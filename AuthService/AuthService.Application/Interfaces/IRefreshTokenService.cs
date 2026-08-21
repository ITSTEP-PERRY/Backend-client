namespace AuthService.Application.Interfaces;

public interface IRefreshTokenService
{
    string GenerateToken();
    string HashToken(string token);
    TimeSpan GetLifetime(bool rememberMe);
    bool IsRememberMeLifetime(TimeSpan lifetime);
}
