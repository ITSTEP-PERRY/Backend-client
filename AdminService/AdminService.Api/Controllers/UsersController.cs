using AdminService.Api.Authorization;
using AdminService.Application.DTOs.Users;
using AdminService.Application.Interfaces;
using AdminService.Application.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminService.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = AdminAuthorizationPolicies.AdminAccess)]
public sealed class UsersController(IUserAdministrationService users) : ControllerBase
{
    [HttpGet]
    public Task<PaginatedUsersResponse> GetUsers([FromQuery] GetUsersRequest request, CancellationToken ct) =>
        users.GetUsersAsync(request, ct);

    [HttpGet("{id:guid}")]
    public Task<UserResponse> GetUser(Guid id, CancellationToken ct) => users.GetUserAsync(id, ct);

    [HttpPatch("{id:guid}/role")]
    public Task<UserResponse> UpdateRole(Guid id, UpdateUserRoleRequest request, CancellationToken ct) =>
        users.UpdateRoleAsync(GetActorId(), id, request.Role!.Value, ct);

    [HttpPatch("{id:guid}/status")]
    public Task<UserResponse> UpdateStatus(Guid id, UpdateUserStatusRequest request, CancellationToken ct) =>
        users.UpdateStatusAsync(GetActorId(), id, request.Status!.Value, ct);

    private Guid GetActorId()
    {
        var subject = User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(subject, out var actorId))
            throw new AdminServiceException("UNAUTHORIZED", "Потрібна автентифікація.", 401);
        return actorId;
    }
}
