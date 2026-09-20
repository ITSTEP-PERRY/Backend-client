using AuthService.Application.Interfaces;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuthService.Infrastructure.Storage;

public sealed class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";
    public string ContainerName { get; set; } = "avatars";
    public string ConnectionString { get; set; } = string.Empty;
}

public sealed class AzureBlobAvatarStorage(IOptions<BlobStorageOptions> options) : IAvatarStorage
{
    private readonly BlobStorageOptions _options = options.Value;

    public async Task UploadAsync(string blobName, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var blob = Client().GetBlobClient(blobName);
        await blob.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        }, cancellationToken);
    }

    public async Task<AvatarFile> OpenReadAsync(string blobName, CancellationToken cancellationToken = default)
    {
        var download = await Client().GetBlobClient(blobName).DownloadStreamingAsync(cancellationToken: cancellationToken);
        return new AvatarFile(download.Value.Content, download.Value.Details.ContentType);
    }

    public async Task DeleteAsync(string blobName, CancellationToken cancellationToken = default) =>
        await Client().GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: cancellationToken);

    private BlobContainerClient Client()
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString) || string.IsNullOrWhiteSpace(_options.ContainerName))
            throw new InvalidOperationException("Blob storage is not configured.");
        return new BlobContainerClient(_options.ConnectionString, _options.ContainerName);
    }
}

public sealed class AccountOperationLogger(ILogger<AccountOperationLogger> logger) : IAccountOperationLogger
{
    public void AvatarCleanupFailed(Guid userId, string operation) =>
        logger.LogWarning("Avatar cleanup failed for user {UserId} during {Operation}.", userId, operation);
}
