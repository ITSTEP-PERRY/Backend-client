using System.Net.Http.Headers;
using System.Net.Http.Json;
using AdminService.Application.DTOs.Users;
using AdminService.Application.Exceptions;
using AdminService.Application.Interfaces;
using AdminService.Application.Models;

namespace AdminService.Infrastructure.AuthService;

internal sealed class AuthServiceClient(HttpClient httpClient, IServiceTokenProvider tokens) : IAuthServiceClient
{
    internal const string ServiceTokenClientName = "AuthService.ServiceToken";

    public async Task<PaginatedUsersResponse> GetUsersAsync(
        GetUsersRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"page={request.Page}", $"pageSize={request.PageSize}",
            $"sortBy={Uri.EscapeDataString(request.SortBy)}",
            $"sortDirection={Uri.EscapeDataString(request.SortDirection)}"
        };
        if (!string.IsNullOrWhiteSpace(request.Search)) query.Add($"search={Uri.EscapeDataString(request.Search.Trim())}");
        if (request.Role.HasValue) query.Add($"role={request.Role.Value}");
        if (request.Status.HasValue) query.Add($"status={request.Status.Value}");
        if (request.EmailVerified.HasValue) query.Add($"emailVerified={request.EmailVerified.Value.ToString().ToLowerInvariant()}");
        var response = await SendAsync<AuthPaginatedUsersResponse>(
            HttpMethod.Get, $"internal/users?{string.Join('&', query)}", null, cancellationToken);
        return new PaginatedUsersResponse
        {
            Page = response.Page,
            PageSize = response.PageSize,
            TotalCount = response.TotalCount,
            Items = response.Items.Select(Map).ToArray()
        };
    }

    public async Task<UserResponse> GetUserAsync(Guid id, CancellationToken cancellationToken = default) =>
        Map(await SendAsync<AuthUserResponse>(HttpMethod.Get, $"internal/users/{id}", null, cancellationToken));

    public async Task<UserResponse> UpdateRoleAsync(
        Guid actorId,
        Guid id,
        UserRole role,
        CancellationToken cancellationToken = default) =>
        Map(await SendAsync<AuthUserResponse>(HttpMethod.Patch, $"internal/users/{id}/role",
            new AuthUpdateRoleRequest { Role = role.ToString() }, cancellationToken, actorId));

    public async Task<UserResponse> UpdateStatusAsync(
        Guid actorId,
        Guid id,
        UserStatus status,
        CancellationToken cancellationToken = default) =>
        Map(await SendAsync<AuthUserResponse>(HttpMethod.Patch, $"internal/users/{id}/status",
            new AuthUpdateStatusRequest { Status = status.ToString() }, cancellationToken, actorId));

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken,
        Guid? actorId = null)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
                await tokens.GetTokenAsync(cancellationToken));
            if (actorId.HasValue)
                request.Headers.Add("X-Admin-Actor-Id", actorId.Value.ToString());
            if (body is not null) request.Content = JsonContent.Create(body);

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                    throw await AuthServiceErrorMapper.ServiceAuthenticationFailedAsync(response, cancellationToken);
                throw await AuthServiceErrorMapper.FromResponseAsync(response, cancellationToken);
            }

            return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
                ?? throw AuthServiceErrorMapper.InvalidResponse();
        }
        catch (AdminServiceException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is HttpRequestException ||
            exception is TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw AuthServiceErrorMapper.Unavailable(exception);
        }
        catch (System.Text.Json.JsonException)
        {
            throw AuthServiceErrorMapper.InvalidResponse();
        }
    }

    private static UserResponse Map(AuthUserResponse user)
    {
        if (!Enum.TryParse<UserRole>(user.Role, true, out var role) ||
            !Enum.TryParse<UserStatus>(user.Status, true, out var status))
            throw AuthServiceErrorMapper.InvalidResponse();

        return new UserResponse
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            EmailVerified = user.EmailVerified,
            Role = role,
            Status = status,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}
