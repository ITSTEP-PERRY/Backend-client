using AuthService.Application.DTOs.Users;
using AuthService.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
public sealed class AdminUsersController(IUserManagementService users) : ControllerBase
{
    [HttpGet]
    public Task<PaginatedUsersResponse> GetUsers([FromQuery] GetUsersRequest request, CancellationToken ct) =>
        users.GetUsersAsync(request.Page, request.PageSize, ct);

    [HttpGet("{id:guid}")]
    public Task<UserManagementResponse> GetUser(Guid id, CancellationToken ct) =>
        users.GetUserAsync(id, ct);

    [HttpPatch("{id:guid}/role")]
    public Task<UserManagementResponse> UpdateRole(Guid id, UpdateUserRoleRequest request, CancellationToken ct) =>
        users.UpdateRoleAsync(id, request.Role, ct);

    [HttpPatch("{id:guid}/status")]
    public Task<UserManagementResponse> UpdateStatus(Guid id, UpdateUserStatusRequest request, CancellationToken ct) =>
        users.UpdateStatusAsync(id, request.Status, ct);
}
