namespace AuthService.Infrastructure.Authentication;

public sealed class InternalJwtOptions
{
    public const string SectionName = "InternalJwt";
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningSecret { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 5;
    public List<InternalServiceCredential> Services { get; set; } = [];
}

public sealed class InternalServiceCredential
{
    public string Name { get; set; } = string.Empty;
    public string CredentialHash { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = [];
}
