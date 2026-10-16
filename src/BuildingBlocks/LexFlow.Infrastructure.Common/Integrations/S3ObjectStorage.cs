using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Infrastructure.Common.HealthChecks;

namespace LexFlow.Infrastructure.Common.Integrations;

/// <summary>Provisionamento do armazenamento (usado pelo MigrationService no ambiente local).</summary>
public interface IObjectStorageProvisioner
{
    /// <summary>Cria o bucket configurado, se ainda não existir.</summary>
    Task EnsureBucketAsync(CancellationToken cancellationToken);
}

/// <summary>Implementação S3 (RustFS local, R2, B2, AWS S3). Bucket configurado em <c>Storage:Bucket</c>.</summary>
internal sealed class S3ObjectStorage : IObjectStorage, IObjectStorageProvisioner, IDisposable
{
    public static readonly TimeSpan DefaultValidity = TimeSpan.FromMinutes(5);

    private readonly AmazonS3Client _client;
    private readonly ObjectStorageSettings _settings;
    private readonly IClock _clock;

    public S3ObjectStorage(ObjectStorageSettings settings, IClock clock)
    {
        _settings = settings;
        _clock = clock;
        _client = new AmazonS3Client(
            new BasicAWSCredentials(settings.AccessKey, settings.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = settings.ServiceUrl!.ToString(),
                AuthenticationRegion = settings.Region,
                ForcePathStyle = true,
            });
    }

    private string Bucket => _settings.Bucket;

    private Amazon.S3.Protocol Protocol =>
        string.Equals(_settings.ServiceUrl!.Scheme, "http", StringComparison.OrdinalIgnoreCase) ? Amazon.S3.Protocol.HTTP : Amazon.S3.Protocol.HTTPS;

    public async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        var buckets = await _client.ListBucketsAsync(cancellationToken);
        if (buckets.Buckets?.Any(b => string.Equals(b.BucketName, Bucket, StringComparison.Ordinal)) != true)
        {
            await _client.PutBucketAsync(Bucket, cancellationToken);
        }
    }

    public async Task PutAsync(ObjectKey key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        await _client.PutObjectAsync(
            new PutObjectRequest { BucketName = Bucket, Key = key.Path, InputStream = content, ContentType = contentType, AutoCloseStream = false },
            cancellationToken);
    }

    public async Task<Stream> OpenReadAsync(ObjectKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        var response = await _client.GetObjectAsync(Bucket, key.Path, cancellationToken);
        return response.ResponseStream;
    }

    public async Task<bool> ExistsAsync(ObjectKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        try
        {
            await _client.GetObjectMetadataAsync(Bucket, key.Path, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task DeleteAsync(ObjectKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        await _client.DeleteObjectAsync(Bucket, key.Path, cancellationToken);
    }

    public Task<Uri> CreateDownloadUrlAsync(ObjectKey key, TimeSpan? validity = null) =>
        PresignAsync(key, HttpVerb.GET, contentType: null, validity);

    public Task<Uri> CreateUploadUrlAsync(ObjectKey key, string contentType, TimeSpan? validity = null) =>
        PresignAsync(key, HttpVerb.PUT, contentType, validity);

    private async Task<Uri> PresignAsync(ObjectKey key, HttpVerb verb, string? contentType, TimeSpan? validity)
    {
        ArgumentNullException.ThrowIfNull(key);
        var request = new GetPreSignedUrlRequest
        {
            BucketName = Bucket,
            Key = key.Path,
            Verb = verb,
            Protocol = Protocol,
            Expires = _clock.UtcNow.Add(validity ?? DefaultValidity).UtcDateTime,
        };
        if (contentType is not null)
        {
            request.ContentType = contentType;
        }

        return new Uri(await _client.GetPreSignedURLAsync(request));
    }

    public void Dispose() => _client.Dispose();
}
