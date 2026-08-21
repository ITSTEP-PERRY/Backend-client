namespace AuthService.Application.DTOs.Auth;

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public int ExpiresIn { get; set; }

    public UserResponse User { get; set; } = null!;

    [System.Text.Json.Serialization.JsonIgnore]
    public string RefreshToken { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonIgnore]
    public DateTime RefreshTokenExpiresAt { get; set; }
}
