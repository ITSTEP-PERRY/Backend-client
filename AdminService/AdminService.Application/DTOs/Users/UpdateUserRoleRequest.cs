using AdminService.Application.Models;

namespace AdminService.Application.DTOs.Users;

public sealed class UpdateUserRoleRequest
{
    public UserRole? Role { get; set; }
}
