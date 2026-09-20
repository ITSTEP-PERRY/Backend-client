namespace AuthService.Application.Interfaces;

public sealed record AvatarFile(Stream Content, string ContentType);

public interface IAvatarStorage
{
    Task UploadAsync(string blobName, Stream content, string contentType, CancellationToken cancellationToken = default);
    Task<AvatarFile> OpenReadAsync(string blobName, CancellationToken cancellationToken = default);
    Task DeleteAsync(string blobName, CancellationToken cancellationToken = default);
}
