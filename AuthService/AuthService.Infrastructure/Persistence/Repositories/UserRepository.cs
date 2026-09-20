using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AuthDbContext _dbContext;

    public UserRepository(AuthDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<User?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        return await _dbContext.Users
            .FirstOrDefaultAsync(
                x => x.Email == normalizedEmail,
                cancellationToken);
    }

    public async Task<User?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public async Task<bool> ExistsByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        return await _dbContext.Users
            .AnyAsync(
                x => x.Email == normalizedEmail,
                cancellationToken);
    }

    public async Task AddAsync(
    User user,
    CancellationToken cancellationToken = default)
    {
        await _dbContext.Users.AddAsync(
            user,
            cancellationToken);
    }

    public Task UpdateAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        _dbContext.Users.Update(user);

        return Task.CompletedTask;
    }

    public Task<User?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default) =>
        _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<User>> GetPageAsync(
        AuthService.Application.DTOs.Users.GetUsersRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyFilters(_dbContext.Users.AsNoTracking(), request);
        var descending = request.SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase);
        query = request.SortBy.ToLowerInvariant() switch
        {
            "updatedat" => descending ? query.OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id) : query.OrderBy(x => x.UpdatedAt).ThenBy(x => x.Id),
            "email" => descending ? query.OrderByDescending(x => x.Email).ThenBy(x => x.Id) : query.OrderBy(x => x.Email).ThenBy(x => x.Id),
            "firstname" => descending ? query.OrderByDescending(x => x.FirstName).ThenBy(x => x.Id) : query.OrderBy(x => x.FirstName).ThenBy(x => x.Id),
            "lastname" => descending ? query.OrderByDescending(x => x.LastName).ThenBy(x => x.Id) : query.OrderBy(x => x.LastName).ThenBy(x => x.Id),
            "role" => descending ? query.OrderByDescending(x => x.Role).ThenBy(x => x.Id) : query.OrderBy(x => x.Role).ThenBy(x => x.Id),
            "status" => descending ? query.OrderByDescending(x => x.Status).ThenBy(x => x.Id) : query.OrderBy(x => x.Status).ThenBy(x => x.Id),
            _ => descending ? query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id) : query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
        };

        return await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(AuthService.Application.DTOs.Users.GetUsersRequest request, CancellationToken cancellationToken = default) =>
        ApplyFilters(_dbContext.Users.AsNoTracking(), request).CountAsync(cancellationToken);

    public Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken = default) =>
        _dbContext.Users.AsNoTracking().CountAsync(
            x => x.Role == UserRole.Admin && x.Status == UserStatus.Active,
            cancellationToken);

    private static IQueryable<User> ApplyFilters(
        IQueryable<User> query,
        AuthService.Application.DTOs.Users.GetUsersRequest request)
    {
        var search = request.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            var normalizedSearch = search.ToLower();
            query = query.Where(x =>
                x.Email.ToLower().Contains(normalizedSearch) ||
                x.FirstName != null && x.FirstName.ToLower().Contains(normalizedSearch) ||
                x.LastName != null && x.LastName.ToLower().Contains(normalizedSearch));
        }
        if (request.Role.HasValue) query = query.Where(x => x.Role == request.Role.Value);
        if (request.Status.HasValue) query = query.Where(x => x.Status == request.Status.Value);
        if (request.EmailVerified.HasValue) query = query.Where(x => x.EmailVerified == request.EmailVerified.Value);
        return query;
    }
}
