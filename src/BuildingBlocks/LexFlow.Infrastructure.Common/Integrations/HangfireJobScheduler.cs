using System.Linq.Expressions;
using Hangfire;
using Hangfire.Client;
using Hangfire.Common;
using Hangfire.Server;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Infrastructure.Common.Context;

namespace LexFlow.Infrastructure.Common.Integrations;

/// <summary>Agenda jobs no Hangfire levando o tenant e a correlação de quem agendou (lidos pelo <see cref="JobContextFilter"/>).</summary>
internal sealed class HangfireJobScheduler(
    IBackgroundJobClient jobs,
    IRecurringJobManager recurring,
    ITenantContext tenant,
    IRequestContext request) : IJobScheduler
{
    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public string Enqueue<TJob>(Expression<Func<TJob, Task>> job)
    {
        using var scope = JobContextFilter.Capture(tenant.OrganizationId, request.CorrelationId);
        return jobs.Enqueue(job);
    }

    public string Schedule<TJob>(Expression<Func<TJob, Task>> job, TimeSpan delay)
    {
        using var scope = JobContextFilter.Capture(tenant.OrganizationId, request.CorrelationId);
        return jobs.Schedule(job, delay);
    }

    public void AddOrUpdateRecurring<TJob>(string recurringJobId, Expression<Func<TJob, Task>> job, string cronExpression) =>
        recurring.AddOrUpdate(recurringJobId, job, cronExpression, new RecurringJobOptions { TimeZone = SaoPaulo });
}

/// <summary>
/// Filtro global do Hangfire: ao criar o job, grava organização e correlação capturadas pelo <see cref="HangfireJobScheduler"/>
/// como parâmetros; ao executar, abre o <see cref="AmbientContext"/> com esses valores (tenant do job, seção 6.2).
/// </summary>
internal sealed class JobContextFilter : JobFilterAttribute, IClientFilter, IServerFilter
{
    public const string OrganizationParameter = "lexflow.organization_id";
    public const string CorrelationParameter = "lexflow.correlation_id";
    private const string ScopeItem = "lexflow.ambient_scope";

    private static readonly AsyncLocal<(Guid? Organization, string? Correlation)?> Pending = new();

    public static IDisposable Capture(Guid? organizationId, string? correlationId)
    {
        Pending.Value = (organizationId, correlationId);
        return new Clear();
    }

    public void OnCreating(CreatingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (Pending.Value is { } captured)
        {
            context.SetJobParameter(OrganizationParameter, captured.Organization);
            context.SetJobParameter(CorrelationParameter, captured.Correlation);
        }
    }

    public void OnCreated(CreatedContext context)
    {
    }

    public void OnPerforming(PerformingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var organization = context.GetJobParameter<Guid?>(OrganizationParameter);
        if (organization is { } org)
        {
            context.Items[ScopeItem] = AmbientContext.Begin(org, context.GetJobParameter<string?>(CorrelationParameter));
        }
    }

    public void OnPerformed(PerformedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Items.TryGetValue(ScopeItem, out var scope) && scope is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private sealed class Clear : IDisposable
    {
        public void Dispose() => Pending.Value = null;
    }
}
