using AuthService.Application.DTOs.Users;
using AuthService.Domain.Entities;

namespace AuthService.Application.Interfaces;

public interface IUserManagementService
{
    Task<PaginatedUsersResponse> GetUsersAsync(GetUsersRequest request, CancellationToken cancellationToken = default);
    Task<UserManagementResponse> GetUserAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UserManagementResponse> UpdateRoleAsync(Guid actorId, Guid id, UserRole role, CancellationToken cancellationToken = default);
    Task<UserManagementResponse> UpdateStatusAsync(Guid actorId, Guid id, UserStatus status, CancellationToken cancellationToken = default);
}
