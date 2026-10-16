using System.Linq.Expressions;

namespace LexFlow.Application.Abstractions.Integrations;

/// <summary>Chave de um objeto: sempre particionada por organização e módulo (<c>/{organizationId}/{modulo}/{id}</c>, seção 6.2).</summary>
public sealed record ObjectKey(Guid OrganizationId, string Module, Guid Id)
{
    public string Path => $"{OrganizationId:D}/{Module.ToLowerInvariant()}/{Id:D}";

    public override string ToString() => Path;
}

/// <summary>Armazenamento de objetos compatível com S3 (seção 8.3). Arquivos grandes não passam pela API.</summary>
public interface IObjectStorage
{
    Task PutAsync(ObjectKey key, Stream content, string contentType, CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(ObjectKey key, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(ObjectKey key, CancellationToken cancellationToken);

    Task DeleteAsync(ObjectKey key, CancellationToken cancellationToken);

    /// <summary>URL pré-assinada de leitura (padrão 5 min, seção 7.3), emitida só depois de checar a permissão.</summary>
    Task<Uri> CreateDownloadUrlAsync(ObjectKey key, TimeSpan? validity = null);

    /// <summary>URL pré-assinada para upload direto do navegador (seção 5.2, item 12).</summary>
    Task<Uri> CreateUploadUrlAsync(ObjectKey key, string contentType, TimeSpan? validity = null);
}

public sealed record EmailMessage(IReadOnlyList<string> To, string Subject, string HtmlBody, string? TextBody = null);

/// <summary>Envio de e-mail transacional (SMTP por padrão; provedores por adaptador, seção 8.3).</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

public enum FileScanStatus
{
    Clean,
    Infected,
}

public sealed record FileScanResult(FileScanStatus Status, string? Signature = null)
{
    public static FileScanResult Clean { get; } = new(FileScanStatus.Clean);

    public static FileScanResult Infected(string signature) => new(FileScanStatus.Infected, signature);
}

/// <summary>Antivírus (ClamAV): arquivos ficam em quarentena até o resultado (seção 7.3).</summary>
public interface IFileScanner
{
    /// <summary>Falha de comunicação com o antivírus lança exceção: o arquivo continua em quarentena e o job é reprocessado.</summary>
    Task<FileScanResult> ScanAsync(Stream content, CancellationToken cancellationToken);
}

/// <summary>
/// Jobs em segundo plano (Hangfire). O tenant e a correlação de quem agenda acompanham o job e são restaurados na execução.
/// </summary>
public interface IJobScheduler
{
    string Enqueue<TJob>(Expression<Func<TJob, Task>> job);

    string Schedule<TJob>(Expression<Func<TJob, Task>> job, TimeSpan delay);

    /// <summary>Job recorrente de plataforma (sem tenant), por expressão cron, no fuso America/Sao_Paulo.</summary>
    void AddOrUpdateRecurring<TJob>(string recurringJobId, Expression<Func<TJob, Task>> job, string cronExpression);
}
