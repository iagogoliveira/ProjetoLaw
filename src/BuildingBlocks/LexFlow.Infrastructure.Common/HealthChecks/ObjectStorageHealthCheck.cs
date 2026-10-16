using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LexFlow.Infrastructure.Common.HealthChecks;

/// <summary>Verifica o armazenamento de objetos pela API S3 (MinIO, R2, B2, AWS).</summary>
internal sealed class ObjectStorageHealthCheck(ObjectStorageSettings settings) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (settings.ServiceUrl is null || string.IsNullOrEmpty(settings.AccessKey) || string.IsNullOrEmpty(settings.SecretKey))
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Storage:ServiceUrl, AccessKey e SecretKey são obrigatórios.");
        }

        try
        {
            var config = new AmazonS3Config
            {
                ServiceURL = settings.ServiceUrl.ToString(),
                AuthenticationRegion = settings.Region,
                ForcePathStyle = true,
                Timeout = TimeSpan.FromSeconds(3),
                MaxErrorRetry = 0,
            };
            using var client = new AmazonS3Client(new BasicAWSCredentials(settings.AccessKey, settings.SecretKey), config);
            await client.ListBucketsAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }
}
