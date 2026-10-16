namespace LexFlow.Api.IntegrationTests;

/// <summary>Mesmas imagens usadas no AppHost (src/Aspire/LexFlow.AppHost/AppHost.cs).</summary>
internal static class TestImages
{
    public const string Postgres = "pgvector/pgvector:pg17-trixie";
    public const string Redis = "redis:8.8";
    public const string RabbitMq = "rabbitmq:4.3-management";
    public const string ObjectStorage = "rustfs/rustfs:1.0.2";
}
