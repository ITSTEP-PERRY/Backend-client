namespace AuthService.Application.DTOs.Users;

public sealed class GetUsersRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
