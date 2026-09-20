using AdminService.Application.DTOs.Users;
using AdminService.Application.Interfaces;
using AdminService.Application.Models;

namespace AdminService.Application.Services;

public sealed class UserAdministrationService(IAuthServiceClient authService) : IUserAdministrationService
{
    public Task<PaginatedUsersResponse> GetUsersAsync(GetUsersRequest request, CancellationToken cancellationToken = default) =>
        authService.GetUsersAsync(request, cancellationToken);

    public Task<UserResponse> GetUserAsync(Guid id, CancellationToken cancellationToken = default) =>
        authService.GetUserAsync(id, cancellationToken);

    public Task<UserResponse> UpdateRoleAsync(Guid actorId, Guid id, UserRole role, CancellationToken cancellationToken = default) =>
        authService.UpdateRoleAsync(actorId, id, role, cancellationToken);

    public Task<UserResponse> UpdateStatusAsync(Guid actorId, Guid id, UserStatus status, CancellationToken cancellationToken = default) =>
        authService.UpdateStatusAsync(actorId, id, status, cancellationToken);
}
