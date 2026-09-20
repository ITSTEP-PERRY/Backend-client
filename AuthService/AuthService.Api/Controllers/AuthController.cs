using AuthService.Application.DTOs.Auth;
using AuthService.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using AuthService.Api.Security;

namespace AuthService.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    [HttpPost("register")]
    [EnableRateLimiting(RateLimitingConfiguration.Register)]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegisterResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _authService.RegisterAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPost("verify-email")]
    [EnableRateLimiting(RateLimitingConfiguration.VerifyEmail)]
    [ProducesResponseType(typeof(VerifyEmailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VerifyEmailResponse>> VerifyEmail(
        [FromBody] VerifyEmailRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _authService.VerifyEmailAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("resend-verification-code")]
    [EnableRateLimiting(RateLimitingConfiguration.ResendVerification)]
    [ProducesResponseType(typeof(ResendVerificationCodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ResendVerificationCodeResponse>> ResendVerificationCode(
        [FromBody] ResendVerificationCodeRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _authService.ResendVerificationCodeAsync(request, cancellationToken);
        return Ok(response);
    }
}
