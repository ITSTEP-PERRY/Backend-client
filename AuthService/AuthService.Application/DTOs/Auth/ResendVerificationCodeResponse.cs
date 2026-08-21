namespace AuthService.Application.DTOs.Auth;

public class ResendVerificationCodeResponse
{
    public string Email { get; set; } = string.Empty;

    public int CodeExpiresInSeconds { get; set; }

    public int ResendAvailableInSeconds { get; set; }
}
