namespace AuthService.Application.DTOs.Auth;

public class UserResponse
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public bool EmailVerified { get; set; }
}