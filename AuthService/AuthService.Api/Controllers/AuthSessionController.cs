using AuthService.Application.DTOs.Auth;
using AuthService.Application.Interfaces;
using AuthService.Application.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using AuthService.Api.Security;

namespace AuthService.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthSessionController(
    IAuthService authService,
    IWebHostEnvironment environment,
    CsrfOriginValidator csrf,
    ICurrentUserContext currentUser) : ControllerBase
{
    private const string RefreshCookieName = "perry_refresh_token";

    [HttpPost("complete-registration")]
    public async Task<ActionResult<CompleteRegistrationResponse>> Complete(CompleteRegistrationRequest request, CancellationToken ct) => Ok(await authService.CompleteRegistrationAsync(request, ct));

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitingConfiguration.Login)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var response = await authService.LoginAsync(request, ct);
        SetCookie(response);
        return Ok(response);
    }

    [HttpPost("refresh")]
    [EnableRateLimiting(RateLimitingConfiguration.Refresh)]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken ct)
    {
        csrf.Validate(Request);
        var response = await authService.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = Request.Cookies[RefreshCookieName] ?? string.Empty }, ct);
        SetCookie(response);
        return Ok(response);
    }

    [HttpPost("logout")]
    [EnableRateLimiting(RateLimitingConfiguration.Refresh)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        csrf.Validate(Request);
        await authService.LogoutAsync(Request.Cookies[RefreshCookieName] ?? string.Empty, ct);
        Response.Cookies.Delete(RefreshCookieName, CookieOptions());
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken ct)
    {
        return Ok(await authService.GetCurrentUserAsync(currentUser.UserId, ct));
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting(RateLimitingConfiguration.PasswordRecovery)]
    public async Task<IActionResult> Forgot(ForgotPasswordRequest request, CancellationToken ct)
    {
        await authService.ForgotPasswordAsync(request, ct);
        return Ok(new { message = "Якщо обліковий запис існує, код скидання пароля надіслано." });
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting(RateLimitingConfiguration.PasswordRecovery)]
    public async Task<IActionResult> Reset(ResetPasswordRequest request, CancellationToken ct)
    {
        await authService.ResetPasswordAsync(request, ct);
        return Ok(new { passwordReset = true });
    }

    private void SetCookie(AuthResponse response) => Response.Cookies.Append(RefreshCookieName, response.RefreshToken, CookieOptions(response.RefreshTokenExpiresAt));
    private CookieOptions CookieOptions(DateTime? expires = null) => new()
    {
        HttpOnly = true, Secure = !environment.IsDevelopment(), SameSite = environment.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.None,
        Expires = expires, Path = "/api/auth"
    };
}
