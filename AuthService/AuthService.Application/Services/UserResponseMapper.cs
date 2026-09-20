using AuthService.Application.DTOs.Auth;
using AuthService.Domain.Entities;

namespace AuthService.Application.Services;

internal static class UserResponseMapper
{
    public static UserResponse Map(User user) => new()
    {
        Id = user.Id, Email = user.Email, FirstName = user.FirstName, LastName = user.LastName,
        EmailVerified = user.EmailVerified, Role = user.Role, Status = user.Status,
        AvatarUrl = user.AvatarBlobName is null ? null : "/api/account/avatar"
    };
}
