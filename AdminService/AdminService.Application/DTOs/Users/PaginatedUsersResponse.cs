namespace AdminService.Application.DTOs.Users;

public sealed class PaginatedUsersResponse
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public IReadOnlyList<UserResponse> Items { get; set; } = [];
}
