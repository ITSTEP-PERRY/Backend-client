using AuthService.Application.DTOs.Internal;
using AuthService.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Api.Controllers;

[ApiController]
[Route("internal/auth")]
public sealed class InternalAuthController(IServiceTokenService serviceTokens) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("token")]
    public ActionResult<ServiceTokenResponse> Token(ServiceTokenRequest request) =>
        Ok(serviceTokens.Issue(request.ServiceName, request.Credential));
}
