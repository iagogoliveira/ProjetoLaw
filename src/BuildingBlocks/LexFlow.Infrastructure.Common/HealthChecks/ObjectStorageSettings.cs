namespace LexFlow.Infrastructure.Common.HealthChecks;

/// <summary>Configuração do armazenamento de objetos compatível com S3 (seção <c>Storage</c>).</summary>
public sealed class ObjectStorageSettings
{
    public const string SectionName = "Storage";

    public Uri? ServiceUrl { get; init; }

    public string? AccessKey { get; init; }

    public string? SecretKey { get; init; }

    public string Region { get; init; } = "us-east-1";

    public string Bucket { get; init; } = "lexflow";
}
