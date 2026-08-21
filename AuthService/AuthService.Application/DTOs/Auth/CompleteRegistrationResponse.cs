namespace AuthService.Application.DTOs.Auth;

public sealed class CompleteRegistrationResponse
{
    public bool RegistrationCompleted { get; set; }
    public UserResponse User { get; set; } = null!;
}
