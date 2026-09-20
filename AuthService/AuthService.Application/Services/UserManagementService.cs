using AuthService.Application.DTOs.Users;
using AuthService.Application.Exceptions;
using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;

namespace AuthService.Application.Services;

public sealed class UserManagementService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    IAdminUserMutationLock mutationLock) : IUserManagementService
{
    public async Task<PaginatedUsersResponse> GetUsersAsync(GetUsersRequest request, CancellationToken cancellationToken = default)
    {
        ValidateQuery(request);
        request.Search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        var items = await users.GetPageAsync(request, cancellationToken);
        var totalCount = await users.CountAsync(request, cancellationToken);
        return new PaginatedUsersResponse
        {
            Page = request.Page, PageSize = request.PageSize, TotalCount = totalCount,
            Items = items.Select(Map).ToArray()
        };
    }

    public async Task<UserManagementResponse> GetUserAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdReadOnlyAsync(id, cancellationToken) ?? throw UserNotFound();
        return Map(user);
    }

    public Task<UserManagementResponse> UpdateRoleAsync(
        Guid actorId, Guid id, UserRole role, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(role))
            throw new AuthException(AuthErrorCodes.InvalidUserRole, "User role is invalid.");
        EnsureNotSelf(actorId, id);
        return mutationLock.ExecuteAsync(async ct =>
        {
            var user = await users.GetByIdAsync(id, ct) ?? throw UserNotFound();
            if (user.Role == role) return Map(user);
            if (user.Status == UserStatus.Deleted)
                throw InvalidStatusTransition();
            if (user.Role == UserRole.Admin && user.Status == UserStatus.Active && role != UserRole.Admin)
                await EnsureNotLastActiveAdminAsync(ct);
            user.Role = role;
            user.UpdatedAt = DateTime.UtcNow;
            await users.UpdateAsync(user, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return Map(user);
        }, cancellationToken);
    }

    public Task<UserManagementResponse> UpdateStatusAsync(
        Guid actorId, Guid id, UserStatus status, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(status))
            throw new AuthException(AuthErrorCodes.InvalidUserStatus, "User status is invalid.");
        EnsureNotSelf(actorId, id);
        return mutationLock.ExecuteAsync(async ct =>
        {
            var user = await users.GetByIdAsync(id, ct) ?? throw UserNotFound();
            if (user.Status == status) return Map(user);
            if (user.Status == UserStatus.Deleted)
                throw InvalidStatusTransition();
            if (user.Role == UserRole.Admin && user.Status == UserStatus.Active && status != UserStatus.Active)
                await EnsureNotLastActiveAdminAsync(ct);

            var now = DateTime.UtcNow;
            user.Status = status;
            user.UpdatedAt = now;
            if (status is UserStatus.Blocked or UserStatus.Deleted)
                await refreshTokens.RevokeAllActiveByUserIdAsync(user.Id, now, ct);
            await users.UpdateAsync(user, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return Map(user);
        }, cancellationToken);
    }

    private static void EnsureNotSelf(Guid actorId, Guid targetId)
    {
        if (actorId == Guid.Empty || actorId == targetId)
            throw new AuthException(AuthErrorCodes.SelfManagementNotAllowed,
                "Ви не можете змінити роль або статус власного облікового запису.", 409);
    }

    private async Task EnsureNotLastActiveAdminAsync(CancellationToken cancellationToken)
    {
        if (await users.CountActiveAdminsAsync(cancellationToken) <= 1)
            throw new AuthException(AuthErrorCodes.LastAdminProtection,
                "Неможливо змінити цього адміністратора, оскільки він є останнім активним адміністратором.", 409);
    }

    private static AuthException InvalidStatusTransition() => new(
        AuthErrorCodes.InvalidStatusTransition,
        "Логічно видалений обліковий запис не можна повторно активувати або заблокувати.", 409);

    private static void ValidateQuery(GetUsersRequest request)
    {
        if (request.Page < 1 || request.PageSize is < 1 or > 100)
            throw new AuthException("INVALID_PAGINATION", "Номер сторінки та її розмір мають допустимі значення.");
        if (request.Search?.Length > 200)
            throw new AuthException("VALIDATION_ERROR", "Перевірте правильність введених даних.");
        if (request.Role.HasValue && !Enum.IsDefined(request.Role.Value))
            throw new AuthException(AuthErrorCodes.InvalidUserRole, "Вказано недійсну роль користувача.");
        if (request.Status.HasValue && !Enum.IsDefined(request.Status.Value))
            throw new AuthException(AuthErrorCodes.InvalidUserStatus, "Вказано недійсний статус користувача.");
        var sortFields = new[] { "createdAt", "updatedAt", "email", "firstName", "lastName", "role", "status" };
        if (!sortFields.Contains(request.SortBy, StringComparer.OrdinalIgnoreCase) ||
            !(request.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase) ||
              request.SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase)))
            throw new AuthException("VALIDATION_ERROR", "Перевірте правильність введених даних.");
    }

    private static AuthException UserNotFound() => new(AuthErrorCodes.UserNotFound, "User was not found.", 404);

    private static UserManagementResponse Map(User user) => new()
    {
        Id = user.Id, Email = user.Email, FirstName = user.FirstName, LastName = user.LastName,
        EmailVerified = user.EmailVerified, Role = user.Role, Status = user.Status,
        CreatedAt = user.CreatedAt, UpdatedAt = user.UpdatedAt
    };
}
