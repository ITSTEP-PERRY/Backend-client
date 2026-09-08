using AuthService.Application.DTOs.Users;
using AuthService.Application.Interfaces;
using AuthService.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Api.Controllers;

[ApiController]
[Route("internal/users")]
[Authorize(AuthenticationSchemes = InternalAuthConstants.Scheme)]
public sealed class InternalUsersController(IUserManagementService users) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = InternalAuthConstants.UsersReadPolicy)]
    public Task<PaginatedUsersResponse> GetUsers([FromQuery] GetUsersRequest request, CancellationToken ct) =>
        users.GetUsersAsync(request.Page, request.PageSize, ct);

    [HttpGet("{id:guid}")]
    [Authorize(Policy = InternalAuthConstants.UsersReadPolicy)]
    public Task<UserManagementResponse> GetUser(Guid id, CancellationToken ct) =>
        users.GetUserAsync(id, ct);

    [HttpPatch("{id:guid}/role")]
    [Authorize(Policy = InternalAuthConstants.UsersManagePolicy)]
    public Task<UserManagementResponse> UpdateRole(Guid id, UpdateUserRoleRequest request, CancellationToken ct) =>
        users.UpdateRoleAsync(id, request.Role, ct);

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = InternalAuthConstants.UsersManagePolicy)]
    public Task<UserManagementResponse> UpdateStatus(Guid id, UpdateUserStatusRequest request, CancellationToken ct) =>
        users.UpdateStatusAsync(id, request.Status, ct);
}
