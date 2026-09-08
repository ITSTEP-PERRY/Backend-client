using AuthService.Domain.Entities;

namespace AuthService.Application.DTOs.Users;

public sealed class UpdateUserRoleRequest
{
    public UserRole Role { get; set; }
}
