namespace AuthService.Application.DTOs.Auth;

public class VerifyEmailResponse
{
    public bool EmailVerified { get; set; }

    public string Email { get; set; } = string.Empty;

    public string RegistrationToken { get; set; } = string.Empty;
}
