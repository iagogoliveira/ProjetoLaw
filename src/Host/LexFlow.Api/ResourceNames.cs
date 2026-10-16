namespace LexFlow.Api;

/// <summary>Nomes das conexões injetadas pelo AppHost (<c>ConnectionStrings:{nome}</c>).</summary>
public static class ResourceNames
{
    public const string Database = "lexflow";
    public const string Redis = "redis";
    public const string RabbitMq = "rabbit";
    public const string Blobs = "blobs";
}
