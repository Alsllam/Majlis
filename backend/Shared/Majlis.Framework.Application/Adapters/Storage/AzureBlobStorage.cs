using Azure.Identity;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Majlis.Framework.Application.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Majlis.Framework.Application.Adapters.Storage;

/// <summary>Cloud adapter: Azure Blob Storage (Azurite locally). The only place the Azure Storage SDK is used.</summary>
public sealed class AzureBlobStorage : IBlobStorage
{
    private readonly BlobServiceClient _client;
    private readonly StorageSharedKeyCredential? _sharedKey;
    private readonly BlobStorageOptions _options;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<Task>> _ensured = new();

    public AzureBlobStorage(IOptions<BlobStorageOptions> options)
    {
        _options = options.Value;
        if (!string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            _client = new BlobServiceClient(_options.ConnectionString);
            _sharedKey = ParseSharedKey(_options.ConnectionString);
        }
        else if (!string.IsNullOrWhiteSpace(_options.AccountUrl))
        {
            // Managed identity (Storage Blob Data Contributor); SAS tokens are then signed with a user delegation key.
            _client = new BlobServiceClient(new Uri(_options.AccountUrl), new DefaultAzureCredential());
        }
        else
        {
            throw new InvalidOperationException("BlobStorage:ConnectionString or BlobStorage:AccountUrl is required.");
        }
    }

    public async Task<PresignedUrl> CreateUploadUrlAsync(string container, string path, string contentType, TimeSpan lifetime, CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(container, cancellationToken);
        var expires = DateTimeOffset.UtcNow.Add(lifetime);
        var builder = new BlobSasBuilder(BlobSasPermissions.Create | BlobSasPermissions.Write, expires)
        {
            BlobContainerName = container,
            BlobName = path,
            ContentType = contentType,
        };
        return new PresignedUrl(await SignAsync(container, path, builder, cancellationToken), expires);
    }

    public async Task<PresignedUrl> CreateDownloadUrlAsync(string container, string path, string fileName, TimeSpan lifetime, CancellationToken cancellationToken = default)
    {
        var expires = DateTimeOffset.UtcNow.Add(lifetime);
        var builder = new BlobSasBuilder(BlobSasPermissions.Read, expires)
        {
            BlobContainerName = container,
            BlobName = path,
            ContentDisposition = $"attachment; filename*=UTF-8''{Uri.EscapeDataString(fileName)}",
        };
        return new PresignedUrl(await SignAsync(container, path, builder, cancellationToken), expires);
    }

    public async Task<StoredBlobInfo?> GetPropertiesAsync(string container, string path, CancellationToken cancellationToken = default)
    {
        var blob = _client.GetBlobContainerClient(container).GetBlobClient(path);
        if (!await blob.ExistsAsync(cancellationToken))
        {
            return null;
        }

        var properties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);
        return new StoredBlobInfo(properties.Value.ContentLength, properties.Value.ContentType);
    }

    public async Task DeleteAsync(string container, string path, CancellationToken cancellationToken = default)
        => await _client.GetBlobContainerClient(container).GetBlobClient(path).DeleteIfExistsAsync(cancellationToken: cancellationToken);

    /// <summary>Browser uploads need CORS on the storage service; set once at startup when origins are configured (dev + Azurite).</summary>
    public async Task ConfigureCorsAsync(CancellationToken cancellationToken = default)
    {
        if (_options.CorsAllowedOrigins.Length == 0)
        {
            return;
        }

        var properties = await _client.GetPropertiesAsync(cancellationToken);
        properties.Value.Cors =
        [
            new BlobCorsRule
            {
                AllowedOrigins = string.Join(',', _options.CorsAllowedOrigins),
                AllowedMethods = "GET,PUT,OPTIONS,HEAD",
                AllowedHeaders = "*",
                ExposedHeaders = "*",
                MaxAgeInSeconds = 3600,
            },
        ];
        await _client.SetPropertiesAsync(properties.Value, cancellationToken);
    }

    private async Task<Uri> SignAsync(string container, string path, BlobSasBuilder builder, CancellationToken cancellationToken)
    {
        var blob = _client.GetBlobContainerClient(container).GetBlobClient(path);
        if (_sharedKey is not null)
        {
            return new BlobUriBuilder(blob.Uri) { Sas = builder.ToSasQueryParameters(_sharedKey) }.ToUri();
        }

        var key = await _client.GetUserDelegationKeyAsync(DateTimeOffset.UtcNow.AddMinutes(-5), builder.ExpiresOn, cancellationToken);
        return new BlobUriBuilder(blob.Uri) { Sas = builder.ToSasQueryParameters(key.Value, _client.AccountName) }.ToUri();
    }

    private Task EnsureContainerAsync(string container, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var ensure = _ensured.GetOrAdd(container, c => new Lazy<Task>(() => _client.GetBlobContainerClient(c).CreateIfNotExistsAsync()));
        return ensure.Value;
    }

    private static StorageSharedKeyCredential? ParseSharedKey(string connectionString)
    {
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0], p => p[1], StringComparer.OrdinalIgnoreCase);
        return parts.TryGetValue("AccountName", out var name) && parts.TryGetValue("AccountKey", out var key)
            ? new StorageSharedKeyCredential(name, key)
            : null;
    }
}

public static class BlobStorageServiceCollectionExtensions
{
    /// <summary>Registers the profile's blob adapter (`cloud` → Azure Blob) from the <c>BlobStorage</c> section.</summary>
    public static IServiceCollection AddMajlisBlobStorage(this IServiceCollection services, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        services.Configure<BlobStorageOptions>(configuration.GetSection("BlobStorage"));
        services.AddSingleton<AzureBlobStorage>();
        services.AddSingleton<IBlobStorage>(sp => sp.GetRequiredService<AzureBlobStorage>());
        return services;
    }
}
