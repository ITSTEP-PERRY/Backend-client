using AdminService.Application.Models;

namespace AdminService.Application.DTOs.Users;

public sealed class GetUsersRequest
{
    public string? Search { get; set; }
    public UserRole? Role { get; set; }
    public UserStatus? Status { get; set; }
    public bool? EmailVerified { get; set; }
    public string SortBy { get; set; } = "createdAt";
    public string SortDirection { get; set; } = "desc";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
