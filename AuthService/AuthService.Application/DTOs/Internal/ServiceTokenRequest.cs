namespace AuthService.Application.DTOs.Internal;

public sealed class ServiceTokenRequest
{
    public string ServiceName { get; set; } = string.Empty;
    public string Credential { get; set; } = string.Empty;
}
