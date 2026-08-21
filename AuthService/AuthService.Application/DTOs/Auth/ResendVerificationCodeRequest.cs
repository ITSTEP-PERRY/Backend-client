namespace AuthService.Application.DTOs.Auth;

public class ResendVerificationCodeRequest
{
    public string Email { get; set; } = string.Empty;
}