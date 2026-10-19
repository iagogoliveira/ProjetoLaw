using System.Collections.Concurrent;
using System.Linq.Expressions;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.SharedKernel.Messaging;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace LexFlow.Api.IntegrationTests;

/// <summary>API em memória configurada com as dependências do <see cref="DependenciesFixture"/>.</summary>
/// <param name="isolateMessaging">
/// Desliga transportes e persistência do Wolverine (que conectam ao subir) nos testes que derrubam dependências de propósito.
/// </param>
/// <param name="testAuthentication">Esquema de teste por cabeçalho (padrão) ou só a validação real de tokens OIDC.</param>
/// <param name="replaceServices">Troca serviços da API (ex.: o antivírus), no mesmo contêiner usado por <see cref="RunJobsAsync"/>.</param>
public sealed class LexFlowApiFactory(
    IReadOnlyDictionary<string, string> settings,
    bool isolateMessaging = false,
    bool testAuthentication = true,
    Action<IServiceCollection>? replaceServices = null)
    : WebApplicationFactory<Program>
{
    /// <summary>Eventos de integração publicados pela API desta fábrica.</summary>
    public ConcurrentQueue<IntegrationEvent> PublishedEvents { get; } = new();

    /// <summary>Jobs enfileirados pela API desta fábrica (não há Worker nos testes): rode com <see cref="RunJobsAsync"/>.</summary>
    public RecordingJobScheduler Jobs { get; } = new();

    /// <summary>Executa os jobs enfileirados (e os que eles enfileirarem), cada um no escritório informado.</summary>
    public async Task RunJobsAsync(Guid organizationId)
    {
        while (Jobs.TryDequeue(out var job))
        {
            using var ambient = AmbientContext.Begin(organizationId, "test-jobs");
            await using var scope = Services.CreateAsyncScope();

            // Na API o escritório vem da requisição; o Worker o restaura do job. Aqui, pelo override do escopo.
            using var tenant = scope.ServiceProvider.GetRequiredService<TenantOverride>().BeginForNewOrganization(organizationId);
            var target = scope.ServiceProvider.GetRequiredService(job.Parameters[0].Type);
            await (Task)job.Compile().DynamicInvoke(target)!;
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // UseSetting chega ao Program.cs antes de ele ler a configuração (minimal hosting).
        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }

        // Autentica quando a requisição traz X-Test-User (sem ele, segue anônima → 401 nos endpoints protegidos).
        // Com testAuthentication = false, vale só a validação real do OpenIddict (tokens emitidos pelo fluxo OIDC).
        if (testAuthentication)
        {
            builder.ConfigureTestServices(services => services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, null));
        }

        // Os eventos continuam indo para o outbox real; a cópia permite verificar o que cada endpoint pediu.
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(PublishedEvents);
            services.Decorate<IIntegrationEventPublisher, RecordingPublisher>();
            services.AddSingleton<IJobScheduler>(Jobs);
            replaceServices?.Invoke(services);
        });

        if (isolateMessaging)
        {
            builder.ConfigureTestServices(services =>
            {
                services.DisableAllExternalWolverineTransports();
                services.DisableAllWolverineMessagePersistence();
            });
        }
    }
}

/// <summary>Guarda os jobs pedidos, para o teste executá-los quando quiser.</summary>
public sealed class RecordingJobScheduler : IJobScheduler
{
    private readonly ConcurrentQueue<LambdaExpression> _queue = new();

    public string Enqueue<TJob>(Expression<Func<TJob, Task>> job)
    {
        _queue.Enqueue(job);
        return Guid.CreateVersion7().ToString("N");
    }

    public string Schedule<TJob>(Expression<Func<TJob, Task>> job, TimeSpan delay) => Enqueue(job);

    public void AddOrUpdateRecurring<TJob>(string recurringJobId, Expression<Func<TJob, Task>> job, string cronExpression)
    {
    }

    internal bool TryDequeue(out LambdaExpression job) => _queue.TryDequeue(out job!);
}

/// <summary>Repassa ao publicador real e guarda uma cópia do evento.</summary>
internal sealed class RecordingPublisher(IIntegrationEventPublisher inner, ConcurrentQueue<IntegrationEvent> published) : IIntegrationEventPublisher
{
    public void Publish<TEvent>(TEvent integrationEvent)
        where TEvent : IntegrationEvent
    {
        published.Enqueue(integrationEvent);
        inner.Publish(integrationEvent);
    }
}
