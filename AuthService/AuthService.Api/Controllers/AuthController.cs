using AuthService.Application.DTOs.Auth;
using AuthService.Application.Exceptions;
using AuthService.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegisterResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _authService.RegisterAsync(request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (DuplicateEmailException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpPost("verify-email")]
    [ProducesResponseType(typeof(VerifyEmailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VerifyEmailResponse>> VerifyEmail(
        [FromBody] VerifyEmailRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _authService.VerifyEmailAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (EmailVerificationException exception)
        {
            return BadRequest(new
            {
                code = exception.ErrorCode,
                message = exception.Message
            });
        }
    }

    [HttpPost("resend-verification-code")]
    [ProducesResponseType(typeof(ResendVerificationCodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ResendVerificationCodeResponse>> ResendVerificationCode(
        [FromBody] ResendVerificationCodeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _authService.ResendVerificationCodeAsync(
                request,
                cancellationToken);

            return Ok(response);
        }
        catch (EmailVerificationException exception)
        {
            var error = new
            {
                code = exception.ErrorCode,
                message = exception.Message,
                retryAfterSeconds = exception.RetryAfterSeconds
            };

            if (exception.ErrorCode == EmailVerificationErrorCodes.UserNotFound)
            {
                return NotFound(error);
            }

            if (exception.ErrorCode == EmailVerificationErrorCodes.ResendCooldownActive)
            {
                Response.Headers.RetryAfter = exception.RetryAfterSeconds.ToString();
                return StatusCode(StatusCodes.Status429TooManyRequests, error);
            }

            return BadRequest(error);
        }
    }
}
