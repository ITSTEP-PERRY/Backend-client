using AuthService.Domain.Entities;

namespace AuthService.Application.DTOs.Users;

public sealed class UpdateUserStatusRequest
{
    public UserStatus Status { get; set; }
}
