using AuthService.Api.Security;
using AuthService.Application.DTOs.Account;
using AuthService.Application.DTOs.Auth;
using AuthService.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AuthService.Api.Controllers;

[ApiController]
[Route("api/account")]
[Authorize]
public sealed class AccountController(
    IAccountService accounts,
    ICurrentUserContext currentUser,
    IWebHostEnvironment environment) : ControllerBase
{
    private const string RefreshCookieName = "perry_refresh_token";

    [HttpPatch("name")]
    [EnableRateLimiting(RateLimitingConfiguration.AccountName)]
    public Task<UserResponse> ChangeName(ChangeNameRequest request, CancellationToken ct) =>
        accounts.ChangeNameAsync(currentUser.UserId, request, ct);

    [HttpPost("email/change-request")]
    [EnableRateLimiting(RateLimitingConfiguration.EmailChangeRequest)]
    public async Task<ActionResult<EmailChangeResponse>> StartEmailChange(EmailChangeStartRequest request, CancellationToken ct) =>
        Accepted(await accounts.StartEmailChangeAsync(currentUser.UserId, request, ct));

    [HttpPost("email/change-resend")]
    [EnableRateLimiting(RateLimitingConfiguration.EmailChangeResend)]
    public async Task<ActionResult<EmailChangeResponse>> ResendEmailChange(CancellationToken ct) =>
        Accepted(await accounts.ResendEmailChangeAsync(currentUser.UserId, ct));

    [HttpPost("email/change-verify")]
    [EnableRateLimiting(RateLimitingConfiguration.EmailChangeVerify)]
    public async Task<IActionResult> VerifyEmailChange(EmailChangeVerifyRequest request, CancellationToken ct)
    {
        await accounts.VerifyEmailChangeAsync(currentUser.UserId, request, ct);
        DeleteRefreshCookie();
        return NoContent();
    }

    [HttpPost("password/change")]
    [EnableRateLimiting(RateLimitingConfiguration.ChangePassword)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await accounts.ChangePasswordAsync(currentUser.UserId, request, ct);
        DeleteRefreshCookie();
        return NoContent();
    }

    [HttpDelete]
    [EnableRateLimiting(RateLimitingConfiguration.DeleteAccount)]
    public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountRequest request, CancellationToken ct)
    {
        await accounts.DeleteAccountAsync(currentUser.UserId, request, ct);
        DeleteRefreshCookie();
        return NoContent();
    }

    [HttpPut("avatar")]
    [EnableRateLimiting(RateLimitingConfiguration.AvatarUpload)]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadAvatar([FromForm] AvatarUploadForm form, CancellationToken ct)
    {
        await using var stream = form.File.OpenReadStream();
        var avatarUrl = await accounts.UploadAvatarAsync(currentUser.UserId, stream, form.File.Length, form.File.ContentType, ct);
        return Ok(new { avatarUrl });
    }

    [HttpGet("avatar")]
    public async Task<IActionResult> GetAvatar(CancellationToken ct)
    {
        var avatar = await accounts.GetAvatarAsync(currentUser.UserId, ct);
        return File(avatar.Content, avatar.ContentType, enableRangeProcessing: false);
    }

    [HttpDelete("avatar")]
    [EnableRateLimiting(RateLimitingConfiguration.AvatarDelete)]
    public async Task<IActionResult> DeleteAvatar(CancellationToken ct)
    {
        await accounts.DeleteAvatarAsync(currentUser.UserId, ct);
        return NoContent();
    }

    private void DeleteRefreshCookie() => Response.Cookies.Delete(RefreshCookieName, new CookieOptions
    {
        HttpOnly = true,
        Secure = !environment.IsDevelopment(),
        SameSite = environment.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.None,
        Path = "/api/auth"
    });
}

public sealed class AvatarUploadForm
{
    public IFormFile File { get; set; } = null!;
}
