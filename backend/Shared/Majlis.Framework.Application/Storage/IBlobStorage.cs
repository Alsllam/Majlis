namespace Majlis.Framework.Application.Storage;

/// <summary>
/// File storage behind an adapter (ADR-0006): Azure Blob on the cloud profile, an S3-compatible store on-prem.
/// Uploads go straight from the browser to storage through a short-lived pre-signed url; the module confirms after.
/// </summary>
public interface IBlobStorage
{
    /// <summary>A url the browser can PUT the file to (write-only, expires).</summary>
    Task<PresignedUrl> CreateUploadUrlAsync(string container, string path, string contentType, TimeSpan lifetime, CancellationToken cancellationToken = default);

    /// <summary>A url the browser can GET the file from (read-only, expires).</summary>
    Task<PresignedUrl> CreateDownloadUrlAsync(string container, string path, string fileName, TimeSpan lifetime, CancellationToken cancellationToken = default);

    /// <summary>Null when the blob does not exist.</summary>
    Task<StoredBlobInfo?> GetPropertiesAsync(string container, string path, CancellationToken cancellationToken = default);

    Task DeleteAsync(string container, string path, CancellationToken cancellationToken = default);
}

public sealed record PresignedUrl(Uri Url, DateTimeOffset ExpiresAt);

public sealed record StoredBlobInfo(long SizeBytes, string? ContentType);

public sealed class BlobStorageOptions
{
    /// <summary>Connection string (local Azurite or a dev account). Deployed environments use the account url + managed identity.</summary>
    public string? ConnectionString { get; set; }

    public string? AccountUrl { get; set; }

    /// <summary>Origins allowed to PUT/GET blobs from the browser (configured on the storage account at startup when set).</summary>
    public string[] CorsAllowedOrigins { get; set; } = [];
}
