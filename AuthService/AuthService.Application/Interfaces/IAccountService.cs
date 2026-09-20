using AuthService.Application.DTOs.Account;
using AuthService.Application.DTOs.Auth;

namespace AuthService.Application.Interfaces;

public interface IAccountService
{
    Task<UserResponse> ChangeNameAsync(Guid userId, ChangeNameRequest request, CancellationToken cancellationToken = default);
    Task<EmailChangeResponse> StartEmailChangeAsync(Guid userId, EmailChangeStartRequest request, CancellationToken cancellationToken = default);
    Task<EmailChangeResponse> ResendEmailChangeAsync(Guid userId, CancellationToken cancellationToken = default);
    Task VerifyEmailChangeAsync(Guid userId, EmailChangeVerifyRequest request, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
    Task DeleteAccountAsync(Guid userId, DeleteAccountRequest request, CancellationToken cancellationToken = default);
    Task<string> UploadAvatarAsync(Guid userId, Stream content, long length, string contentType, CancellationToken cancellationToken = default);
    Task<AvatarFile> GetAvatarAsync(Guid userId, CancellationToken cancellationToken = default);
    Task DeleteAvatarAsync(Guid userId, CancellationToken cancellationToken = default);
}
