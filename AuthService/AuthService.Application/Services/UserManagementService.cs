using AuthService.Application.DTOs.Users;
using AuthService.Application.Exceptions;
using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;

namespace AuthService.Application.Services;

public sealed class UserManagementService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork) : IUserManagementService
{
    public async Task<PaginatedUsersResponse> GetUsersAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        ValidatePagination(page, pageSize);
        var items = await users.GetPageAsync(page, pageSize, cancellationToken);
        var totalCount = await users.CountAsync(cancellationToken);
        return new PaginatedUsersResponse
        {
            Page = page, PageSize = pageSize, TotalCount = totalCount,
            Items = items.Select(Map).ToArray()
        };
    }

    public async Task<UserManagementResponse> GetUserAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdReadOnlyAsync(id, cancellationToken) ?? throw UserNotFound();
        return Map(user);
    }

    public async Task<UserManagementResponse> UpdateRoleAsync(Guid id, UserRole role, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(role))
            throw new AuthException(AuthErrorCodes.InvalidUserRole, "User role is invalid.");
        var user = await users.GetByIdAsync(id, cancellationToken) ?? throw UserNotFound();
        user.Role = role;
        user.UpdatedAt = DateTime.UtcNow;
        await users.UpdateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(user);
    }

    public async Task<UserManagementResponse> UpdateStatusAsync(Guid id, UserStatus status, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(status))
            throw new AuthException(AuthErrorCodes.InvalidUserStatus, "User status is invalid.");
        var user = await users.GetByIdAsync(id, cancellationToken) ?? throw UserNotFound();
        if (user.Status == status) return Map(user);

        var now = DateTime.UtcNow;
        user.Status = status;
        user.UpdatedAt = now;
        if (status == UserStatus.Deleted)
            await refreshTokens.RevokeAllActiveByUserIdAsync(user.Id, now, cancellationToken);
        await users.UpdateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(user);
    }

    private static void ValidatePagination(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            throw new AuthException("INVALID_PAGINATION", "Page must be at least 1 and pageSize must be between 1 and 100.");
    }

    private static AuthException UserNotFound() => new(AuthErrorCodes.UserNotFound, "User was not found.", 404);

    private static UserManagementResponse Map(User user) => new()
    {
        Id = user.Id, Email = user.Email, FirstName = user.FirstName, LastName = user.LastName,
        EmailVerified = user.EmailVerified, Role = user.Role, Status = user.Status,
        CreatedAt = user.CreatedAt, UpdatedAt = user.UpdatedAt
    };
}
