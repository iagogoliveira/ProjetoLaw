using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Platform.Domain.Jobs;

/// <summary>Estados de uma operação longa (seção 4.4): <c>queued|running|succeeded|failed</c>.</summary>
public enum JobStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
}

public static class JobErrors
{
    public static readonly Error NotFound = Error.NotFound("platform.job_not_found", "Operação não encontrada.");

    public static readonly Error InvalidTransition =
        Error.Conflict("platform.job_invalid_transition", "A operação não está em um estado que permita essa mudança.");
}

/// <summary>
/// Operação assíncrona acompanhada por <c>GET /api/v1/jobs/{id}</c>: a API responde 202 com <c>Location</c> e o
/// Worker atualiza o estado. Registro técnico, fora da trilha de auditoria.
/// </summary>
[NotAudited]
public sealed class Job : Entity<Guid>, ITenantOwned, IAuditable
{
    private Job()
    {
    }

    private Job(Guid id, string type)
        : base(id)
    {
        Type = type;
        Status = JobStatus.Queued;
    }

    public Guid OrganizationId { get; private set; }

    /// <summary>Tipo da operação (ex.: <c>documents.import</c>).</summary>
    public string Type { get; private set; } = string.Empty;

    public JobStatus Status { get; private set; }

    /// <summary>Recurso produzido (ex.: <c>/api/v1/documents/{id}</c>), quando houver.</summary>
    public string? ResultLocation { get; private set; }

    /// <summary>Código estável do erro (<c>modulo.erro</c>), quando falhar.</summary>
    public string? ErrorCode { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public static Job Enqueue(Guid id, string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        return new Job(id, type);
    }

    public Result Start(DateTimeOffset now)
    {
        if (Status != JobStatus.Queued)
        {
            return JobErrors.InvalidTransition;
        }

        Status = JobStatus.Running;
        StartedAt = now;
        return Result.Success;
    }

    public Result Succeed(DateTimeOffset now, string? resultLocation)
    {
        if (Status != JobStatus.Running)
        {
            return JobErrors.InvalidTransition;
        }

        Status = JobStatus.Succeeded;
        ResultLocation = resultLocation;
        CompletedAt = now;
        return Result.Success;
    }

    public Result Fail(DateTimeOffset now, string errorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        if (Status is JobStatus.Succeeded or JobStatus.Failed)
        {
            return JobErrors.InvalidTransition;
        }

        Status = JobStatus.Failed;
        ErrorCode = errorCode;
        CompletedAt = now;
        return Result.Success;
    }
}
