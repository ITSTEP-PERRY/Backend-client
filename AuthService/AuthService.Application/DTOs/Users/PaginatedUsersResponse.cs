namespace AuthService.Application.DTOs.Users;

public sealed class PaginatedUsersResponse
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public IReadOnlyList<UserManagementResponse> Items { get; set; } = [];
}
