namespace AuthService.Application.DTOs.Auth;

public class RegisterResponse
{
    public Guid UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public bool RequiresEmailVerification { get; set; }

    public int CodeExpiresInSeconds { get; set; }
}