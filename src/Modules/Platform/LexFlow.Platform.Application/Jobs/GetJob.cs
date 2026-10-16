using FluentValidation;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Platform.Application.Abstractions;
using LexFlow.Platform.Domain.Jobs;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Platform.Application.Jobs;

/// <summary>Estado de uma operação longa para <c>GET /api/v1/jobs/{id}</c> (seção 4.4).</summary>
public sealed record JobResponse(
    Guid Id,
    string Type,
    string Status,
    string? ResultLocation,
    string? ErrorCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt);

public sealed record GetJobQuery(Guid Id) : IQuery<JobResponse>;

internal sealed class GetJobValidator : AbstractValidator<GetJobQuery>
{
    public GetJobValidator()
    {
        RuleFor(q => q.Id).NotEmpty().WithErrorCode("platform.job_id_required");
    }
}

/// <summary>O filtro de tenant torna o job de outra organização invisível: o resultado é 404, nunca 403.</summary>
internal sealed class GetJobHandler(IPlatformDbContext db) : IQueryHandler<GetJobQuery, JobResponse>
{
    public async Task<Result<JobResponse>> Handle(GetJobQuery query, CancellationToken cancellationToken)
    {
        var job = await db.Jobs.AsNoTracking()
            .Where(j => j.Id == query.Id)
            .Select(j => new { j.Id, j.Type, j.Status, j.ResultLocation, j.ErrorCode, j.CreatedAt, j.StartedAt, j.CompletedAt })
            .SingleOrDefaultAsync(cancellationToken);

        return job is null
            ? JobErrors.NotFound
            : new JobResponse(job.Id, job.Type, ToApiStatus(job.Status), job.ResultLocation, job.ErrorCode, job.CreatedAt, job.StartedAt, job.CompletedAt);
    }

    internal static string ToApiStatus(JobStatus status) => status switch
    {
        JobStatus.Queued => "queued",
        JobStatus.Running => "running",
        JobStatus.Succeeded => "succeeded",
        _ => "failed",
    };
}
