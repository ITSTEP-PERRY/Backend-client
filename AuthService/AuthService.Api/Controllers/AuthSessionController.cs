using System.IdentityModel.Tokens.Jwt;
using AuthService.Application.DTOs.Auth;
using AuthService.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthSessionController(IAuthService authService, IWebHostEnvironment environment) : ControllerBase
{
    private const string RefreshCookieName = "perry_refresh_token";

    [HttpPost("complete-registration")]
    public async Task<ActionResult<CompleteRegistrationResponse>> Complete(CompleteRegistrationRequest request, CancellationToken ct) => Ok(await authService.CompleteRegistrationAsync(request, ct));

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var response = await authService.LoginAsync(request, ct);
        SetCookie(response);
        return Ok(response);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken ct)
    {
        var response = await authService.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = Request.Cookies[RefreshCookieName] ?? string.Empty }, ct);
        SetCookie(response);
        return Ok(response);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await authService.LogoutAsync(Request.Cookies[RefreshCookieName] ?? string.Empty, ct);
        Response.Cookies.Delete(RefreshCookieName, CookieOptions());
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken ct)
    {
        var subject = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(subject, out var id) ? Ok(await authService.GetCurrentUserAsync(id, ct)) : Unauthorized();
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> Forgot(ForgotPasswordRequest request, CancellationToken ct)
    {
        await authService.ForgotPasswordAsync(request, ct);
        return Ok(new { message = "If an account exists for this email, a reset code has been sent." });
    }

    [HttpPost("reset-password")]
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
