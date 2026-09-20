using AdminService.Application.DTOs.Users;
using AdminService.Application.Models;

namespace AdminService.Application.Interfaces;

public interface IAuthServiceClient
{
    Task<PaginatedUsersResponse> GetUsersAsync(GetUsersRequest request, CancellationToken cancellationToken = default);
    Task<UserResponse> GetUserAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UserResponse> UpdateRoleAsync(Guid actorId, Guid id, UserRole role, CancellationToken cancellationToken = default);
    Task<UserResponse> UpdateStatusAsync(Guid actorId, Guid id, UserStatus status, CancellationToken cancellationToken = default);
}
