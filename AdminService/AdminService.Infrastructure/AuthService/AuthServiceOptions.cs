namespace AdminService.Infrastructure.AuthService;

public sealed class AuthServiceOptions
{
    public const string SectionName = "AuthService";
    public string BaseUrl { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 10;
    public string ServiceName { get; set; } = string.Empty;
    public string ServiceCredential { get; set; } = string.Empty;
}
