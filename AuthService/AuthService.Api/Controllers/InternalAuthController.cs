using AuthService.Application.DTOs.Internal;
using AuthService.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using AuthService.Api.Security;

namespace AuthService.Api.Controllers;

[ApiController]
[Route("internal/auth")]
public sealed class InternalAuthController(IServiceTokenService serviceTokens) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("token")]
    [EnableRateLimiting(RateLimitingConfiguration.InternalToken)]
    public ActionResult<ServiceTokenResponse> Token(ServiceTokenRequest request) =>
        Ok(serviceTokens.Issue(request.ServiceName, request.Credential));
}
