using AdminService.Application.Models;

namespace AdminService.Application.DTOs.Users;

public sealed class UpdateUserStatusRequest
{
    public UserStatus? Status { get; set; }
}
