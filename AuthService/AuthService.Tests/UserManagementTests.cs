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

        var result = await fixture.Service.GetUsersAsync(2, 10);

        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(25, result.TotalCount);
        Assert.Equal(10, result.Items.Count);
    }

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
        var result = await fixture.Service.UpdateRoleAsync(user.Id, UserRole.Admin);
        Assert.Equal(UserRole.Admin, result.Role);
        Assert.Equal(1, fixture.UnitOfWork.Saves);
    }

    [Fact]
    public async Task UpdateRole_RejectsUnknownRole()
    {
        var fixture = new Fixture([CreateUser(1)]);
        var exception = await Assert.ThrowsAsync<AuthException>(() =>
            fixture.Service.UpdateRoleAsync(fixture.Users.Items[0].Id, (UserRole)999));
        Assert.Equal(AuthErrorCodes.InvalidUserRole, exception.ErrorCode);
    }

    [Fact]
    public async Task UpdateStatus_DeletedRevokesRefreshTokens()
    {
        var user = CreateUser(1);
        var fixture = new Fixture([user]);
        var result = await fixture.Service.UpdateStatusAsync(user.Id, UserStatus.Deleted);
        Assert.Equal(UserStatus.Deleted, result.Status);
        Assert.Equal(user.Id, fixture.RefreshTokens.RevokedUserId);
        Assert.Equal(1, fixture.UnitOfWork.Saves);
    }

    [Fact]
    public async Task UpdateStatus_ActiveDoesNotRevokeRefreshTokens()
    {
        var user = CreateUser(1);
        user.Status = UserStatus.Deleted;
        var fixture = new Fixture([user]);
        var result = await fixture.Service.UpdateStatusAsync(user.Id, UserStatus.Active);
        Assert.Equal(UserStatus.Active, result.Status);
        Assert.Null(fixture.RefreshTokens.RevokedUserId);
    }

    [Fact]
    public async Task UpdateStatus_RejectsUnknownStatus()
    {
        var fixture = new Fixture([CreateUser(1)]);
        var exception = await Assert.ThrowsAsync<AuthException>(() =>
            fixture.Service.UpdateStatusAsync(fixture.Users.Items[0].Id, (UserStatus)999));
        Assert.Equal(AuthErrorCodes.InvalidUserStatus, exception.ErrorCode);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task GetUsers_RejectsInvalidPagination(int page, int pageSize) =>
        await Assert.ThrowsAsync<AuthException>(() => new Fixture([]).Service.GetUsersAsync(page, pageSize));

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
            Service = new UserManagementService(Users, RefreshTokens, UnitOfWork);
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
        public Task<IReadOnlyList<User>> GetPageAsync(int page, int pageSize, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<User>>(items.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
                .Skip((page - 1) * pageSize).Take(pageSize).ToList());
        public Task<int> CountAsync(CancellationToken ct = default) => Task.FromResult(items.Count);
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
}
