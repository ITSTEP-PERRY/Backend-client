namespace AdminService.Infrastructure.AuthService;

internal sealed class ServiceTokenRequest
{
    public string ServiceName { get; set; } = string.Empty;
    public string Credential { get; set; } = string.Empty;
}

internal sealed class ServiceTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
}

internal sealed class AuthUserResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool EmailVerified { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

internal sealed class AuthPaginatedUsersResponse
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public IReadOnlyList<AuthUserResponse> Items { get; set; } = [];
}

internal sealed class AuthUpdateRoleRequest
{
    public string Role { get; set; } = string.Empty;
}

internal sealed class AuthUpdateStatusRequest
{
    public string Status { get; set; } = string.Empty;
}

internal sealed class AuthErrorResponse
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, string[]>? Errors { get; set; }
}
