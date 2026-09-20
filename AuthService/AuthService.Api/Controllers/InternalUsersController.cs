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
    private const string ActorHeader = "X-Admin-Actor-Id";
    [HttpGet]
    [Authorize(Policy = InternalAuthConstants.UsersReadPolicy)]
    public Task<PaginatedUsersResponse> GetUsers([FromQuery] GetUsersRequest request, CancellationToken ct) =>
        users.GetUsersAsync(request, ct);

    [HttpGet("{id:guid}")]
    [Authorize(Policy = InternalAuthConstants.UsersReadPolicy)]
    public Task<UserManagementResponse> GetUser(Guid id, CancellationToken ct) =>
        users.GetUserAsync(id, ct);

    [HttpPatch("{id:guid}/role")]
    [Authorize(Policy = InternalAuthConstants.UsersManagePolicy)]
    public Task<UserManagementResponse> UpdateRole(Guid id, UpdateUserRoleRequest request,
        [FromHeader(Name = ActorHeader)] Guid actorId, CancellationToken ct) =>
        users.UpdateRoleAsync(actorId, id, request.Role!.Value, ct);

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = InternalAuthConstants.UsersManagePolicy)]
    public Task<UserManagementResponse> UpdateStatus(Guid id, UpdateUserStatusRequest request,
        [FromHeader(Name = ActorHeader)] Guid actorId, CancellationToken ct) =>
        users.UpdateStatusAsync(actorId, id, request.Status!.Value, ct);
}
