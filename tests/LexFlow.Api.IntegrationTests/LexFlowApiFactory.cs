using System.Collections.Concurrent;
using LexFlow.Application.Abstractions.Messaging;
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
public sealed class LexFlowApiFactory(IReadOnlyDictionary<string, string> settings, bool isolateMessaging = false, bool testAuthentication = true)
    : WebApplicationFactory<Program>
{
    /// <summary>Eventos de integração publicados pela API desta fábrica.</summary>
    public ConcurrentQueue<IntegrationEvent> PublishedEvents { get; } = new();

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
