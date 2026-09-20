using AuthService.Application.Exceptions;
using AuthService.Application.Interfaces;
using AuthService.Application.Services;
using AuthService.Domain.Entities;
using Xunit;

namespace AuthService.Tests;

public sealed class UserManagementTests
{
    [Fact]
    public async Task GetUsers_ReturnsStablePageAndMetadata()
    {
        var fixture = new Fixture(Enumerable.Range(1, 25).Select(CreateUser).ToList());

        var result = await fixture.Service.GetUsersAsync(new AuthService.Application.DTOs.Users.GetUsersRequest { Page = 2, PageSize = 10 });

        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(25, result.TotalCount);
        Assert.Equal(10, result.Items.Count);
    }

    [Fact]
    public async Task GetUsers_SearchesSupportedFieldsAndTreatsWhitespaceAsEmpty()
    {
        var alpha = CreateUser(1); alpha.FirstName = "Olena";
        var beta = CreateUser(2); beta.Email = "petro@example.com";
        var fixture = new Fixture([alpha, beta]);

        var match = await fixture.Service.GetUsersAsync(new() { Search = "  OLENA " });
        var all = await fixture.Service.GetUsersAsync(new() { Search = "   " });

        Assert.Equal(alpha.Id, Assert.Single(match.Items).Id);
        Assert.Equal(2, all.TotalCount);
    }

    [Fact]
    public async Task GetUsers_SearchWithoutResults_ReturnsEmptyPage() =>
        Assert.Empty((await new Fixture([CreateUser(1)]).Service.GetUsersAsync(new() { Search = "missing" })).Items);

    [Fact]
    public async Task GetUsers_CombinesFilters()
    {
        var admin = CreateUser(1); admin.Role = UserRole.Admin; admin.EmailVerified = true;
        var user = CreateUser(2); user.Role = UserRole.User; user.EmailVerified = false;
        var result = await new Fixture([admin, user]).Service.GetUsersAsync(new()
        { Role = UserRole.Admin, Status = UserStatus.Active, EmailVerified = true });
        Assert.Equal(admin.Id, Assert.Single(result.Items).Id);
    }

    [Fact]
    public async Task GetUsers_SortsAscendingAndDescending()
    {
        var a = CreateUser(1); a.Email = "a@example.com";
        var z = CreateUser(2); z.Email = "z@example.com";
        var fixture = new Fixture([z, a]);
        var asc = await fixture.Service.GetUsersAsync(new() { SortBy = "email", SortDirection = "asc" });
        var desc = await fixture.Service.GetUsersAsync(new() { SortBy = "email", SortDirection = "desc" });
        Assert.Equal(a.Id, asc.Items[0].Id);
        Assert.Equal(z.Id, desc.Items[0].Id);
    }

    [Theory]
    [InlineData("unknown", "asc")]
    [InlineData("email", "sideways")]
    public async Task GetUsers_RejectsInvalidSorting(string sortBy, string direction) =>
        await Assert.ThrowsAsync<AuthException>(() => new Fixture([]).Service.GetUsersAsync(new()
        { SortBy = sortBy, SortDirection = direction }));

    [Fact]
    public async Task GetUser_ReturnsExpectedSafeDto()
    {
        var user = CreateUser(1);
        var fixture = new Fixture([user]);

        var result = await fixture.Service.GetUserAsync(user.Id);

        Assert.Equal(user.Id, result.Id);
        Assert.Equal(user.Email, result.Email);
        Assert.DoesNotContain("Password", result.GetType().GetProperties().Select(x => x.Name));
    }

    [Fact]
    public async Task GetUser_WhenMissing_ReturnsNotFound()
    {
        var exception = await Assert.ThrowsAsync<AuthException>(() =>
            new Fixture([]).Service.GetUserAsync(Guid.NewGuid()));
        Assert.Equal(404, exception.StatusCode);
    }

    [Fact]
    public async Task UpdateRole_ChangesRole()
    {
        var user = CreateUser(1);
        var fixture = new Fixture([user]);
        var result = await fixture.Service.UpdateRoleAsync(Guid.NewGuid(), user.Id, UserRole.Admin);
        Assert.Equal(UserRole.Admin, result.Role);
        Assert.Equal(1, fixture.UnitOfWork.Saves);
    }

    [Fact]
    public async Task UpdateRole_RejectsUnknownRole()
    {
        var fixture = new Fixture([CreateUser(1)]);
        var exception = await Assert.ThrowsAsync<AuthException>(() =>
            fixture.Service.UpdateRoleAsync(Guid.NewGuid(), fixture.Users.Items[0].Id, (UserRole)999));
        Assert.Equal(AuthErrorCodes.InvalidUserRole, exception.ErrorCode);
    }

    [Fact]
    public async Task UpdateStatus_DeletedRevokesRefreshTokens()
    {
        var user = CreateUser(1);
        var fixture = new Fixture([user]);
        var result = await fixture.Service.UpdateStatusAsync(Guid.NewGuid(), user.Id, UserStatus.Deleted);
        Assert.Equal(UserStatus.Deleted, result.Status);
        Assert.Equal(user.Id, fixture.RefreshTokens.RevokedUserId);
        Assert.Equal(1, fixture.UnitOfWork.Saves);
    }

    [Fact]
    public async Task UpdateStatus_BlockedToActiveDoesNotRevokeRefreshTokens()
    {
        var user = CreateUser(1);
        user.Status = UserStatus.Blocked;
        var fixture = new Fixture([user]);
        var result = await fixture.Service.UpdateStatusAsync(Guid.NewGuid(), user.Id, UserStatus.Active);
        Assert.Equal(UserStatus.Active, result.Status);
        Assert.Null(fixture.RefreshTokens.RevokedUserId);
    }

    [Fact]
    public async Task UpdateStatus_RejectsUnknownStatus()
    {
        var fixture = new Fixture([CreateUser(1)]);
        var exception = await Assert.ThrowsAsync<AuthException>(() =>
            fixture.Service.UpdateStatusAsync(Guid.NewGuid(), fixture.Users.Items[0].Id, (UserStatus)999));
        Assert.Equal(AuthErrorCodes.InvalidUserStatus, exception.ErrorCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SelfManagement_IsRejected(bool roleChange)
    {
        var user = CreateUser(1); user.Role = UserRole.Admin;
        var fixture = new Fixture([user]);
        var exception = roleChange
            ? await Assert.ThrowsAsync<AuthException>(() => fixture.Service.UpdateRoleAsync(user.Id, user.Id, UserRole.User))
            : await Assert.ThrowsAsync<AuthException>(() => fixture.Service.UpdateStatusAsync(user.Id, user.Id, UserStatus.Blocked));
        Assert.Equal(AuthErrorCodes.SelfManagementNotAllowed, exception.ErrorCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(UserStatus.Blocked)]
    [InlineData(UserStatus.Deleted)]
    public async Task LastActiveAdmin_IsProtected(UserStatus? targetStatus)
    {
        var admin = CreateUser(1); admin.Role = UserRole.Admin;
        var fixture = new Fixture([admin]);
        var exception = targetStatus is null
            ? await Assert.ThrowsAsync<AuthException>(() => fixture.Service.UpdateRoleAsync(Guid.NewGuid(), admin.Id, UserRole.User))
            : await Assert.ThrowsAsync<AuthException>(() => fixture.Service.UpdateStatusAsync(Guid.NewGuid(), admin.Id, targetStatus.Value));
        Assert.Equal(AuthErrorCodes.LastAdminProtection, exception.ErrorCode);
    }

    [Fact]
    public async Task AnotherAdmin_CanModifyTargetWhenAnotherActiveAdminRemains()
    {
        var actor = CreateUser(1); actor.Role = UserRole.Admin;
        var target = CreateUser(2); target.Role = UserRole.Admin;
        var fixture = new Fixture([actor, target]);
        Assert.Equal(UserRole.User, (await fixture.Service.UpdateRoleAsync(actor.Id, target.Id, UserRole.User)).Role);
    }

    [Theory]
    [InlineData(UserStatus.Active)]
    [InlineData(UserStatus.Blocked)]
    public async Task DeletedUser_CannotTransition(UserStatus targetStatus)
    {
        var user = CreateUser(1); user.Status = UserStatus.Deleted;
        var exception = await Assert.ThrowsAsync<AuthException>(() =>
            new Fixture([user]).Service.UpdateStatusAsync(Guid.NewGuid(), user.Id, targetStatus));
        Assert.Equal(AuthErrorCodes.InvalidStatusTransition, exception.ErrorCode);
    }

    [Fact]
    public async Task BlockingUser_RevokesRefreshTokens()
    {
        var user = CreateUser(1);
        var fixture = new Fixture([user]);
        await fixture.Service.UpdateStatusAsync(Guid.NewGuid(), user.Id, UserStatus.Blocked);
        Assert.Equal(user.Id, fixture.RefreshTokens.RevokedUserId);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task GetUsers_RejectsInvalidPagination(int page, int pageSize) =>
        await Assert.ThrowsAsync<AuthException>(() => new Fixture([]).Service.GetUsersAsync(
            new AuthService.Application.DTOs.Users.GetUsersRequest { Page = page, PageSize = pageSize }));

    private static User CreateUser(int index) => new()
    {
        Id = Guid.NewGuid(), Email = $"user{index}@example.com", PasswordHash = "sensitive",
        FirstName = "Test", LastName = index.ToString(), EmailVerified = true,
        Role = UserRole.User, Status = UserStatus.Active,
        CreatedAt = DateTime.UtcNow.AddMinutes(-index), UpdatedAt = DateTime.UtcNow
    };

    private sealed class Fixture
    {
        public Fixture(List<User> items)
        {
            Users = new MemoryUsers(items);
            RefreshTokens = new RecordingRefreshTokens();
            UnitOfWork = new RecordingUnitOfWork();
            Service = new UserManagementService(Users, RefreshTokens, UnitOfWork, new NoOpMutationLock());
        }
        public UserManagementService Service { get; }
        public MemoryUsers Users { get; }
        public RecordingRefreshTokens RefreshTokens { get; }
        public RecordingUnitOfWork UnitOfWork { get; }
    }

    private sealed class MemoryUsers(List<User> items) : IUserRepository
    {
        public List<User> Items => items;
        public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
            Task.FromResult(items.FirstOrDefault(x => x.Email == email));
        public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(items.FirstOrDefault(x => x.Id == id));
        public Task<User?> GetByIdReadOnlyAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(items.FirstOrDefault(x => x.Id == id));
        public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default) =>
            Task.FromResult(items.Any(x => x.Email == email));
        public Task AddAsync(User user, CancellationToken ct = default) { items.Add(user); return Task.CompletedTask; }
        public Task UpdateAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<User>> GetPageAsync(AuthService.Application.DTOs.Users.GetUsersRequest request, CancellationToken ct = default)
        {
            var filtered = Filter(request);
            var query = (request.SortBy.ToLowerInvariant(), request.SortDirection.ToLowerInvariant()) switch
            {
                ("email", "asc") => filtered.OrderBy(x => x.Email),
                ("email", _) => filtered.OrderByDescending(x => x.Email),
                (_, "asc") => filtered.OrderBy(x => x.CreatedAt),
                _ => filtered.OrderByDescending(x => x.CreatedAt)
            };
            return Task.FromResult<IReadOnlyList<User>>(query.ThenBy(x => x.Id)
                .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList());
        }
        public Task<int> CountAsync(AuthService.Application.DTOs.Users.GetUsersRequest request, CancellationToken ct = default) =>
            Task.FromResult(Filter(request).Count());
        public Task<int> CountActiveAdminsAsync(CancellationToken ct = default) =>
            Task.FromResult(items.Count(x => x.Role == UserRole.Admin && x.Status == UserStatus.Active));
        private IEnumerable<User> Filter(AuthService.Application.DTOs.Users.GetUsersRequest request)
        {
            IEnumerable<User> query = items;
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var search = request.Search.Trim();
                query = query.Where(x => x.Email.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    x.FirstName?.Contains(search, StringComparison.OrdinalIgnoreCase) == true ||
                    x.LastName?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);
            }
            if (request.Role.HasValue) query = query.Where(x => x.Role == request.Role);
            if (request.Status.HasValue) query = query.Where(x => x.Status == request.Status);
            if (request.EmailVerified.HasValue) query = query.Where(x => x.EmailVerified == request.EmailVerified);
            return query;
        }
    }

    private sealed class RecordingRefreshTokens : IRefreshTokenRepository
    {
        public Guid? RevokedUserId { get; private set; }
        public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default) => Task.FromResult<RefreshToken?>(null);
        public Task AddAsync(RefreshToken token, CancellationToken ct = default) => Task.CompletedTask;
        public Task RevokeAllActiveByUserIdAsync(Guid userId, DateTime revokedAt, CancellationToken ct = default)
        { RevokedUserId = userId; return Task.CompletedTask; }
    }

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public int Saves { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken ct = default) { Saves++; return Task.FromResult(1); }
    }

    private sealed class NoOpMutationLock : IAdminUserMutationLock
    {
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default) => action(ct);
    }
}
