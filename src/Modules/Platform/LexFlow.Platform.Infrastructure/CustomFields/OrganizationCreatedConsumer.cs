using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Messaging;
using LexFlow.Organization.Contracts;
using LexFlow.Platform.Application.CustomFields;
using LexFlow.Platform.Infrastructure.Persistence;

namespace LexFlow.Platform.Infrastructure.CustomFields;

/// <summary>
/// No Worker: cria as tabelas auxiliares do sistema, com os itens sugeridos, no escritório recém-criado. Executa com
/// o tenant do evento; o seeder é idempotente, e a reentrega não duplica nada.
/// </summary>
public sealed class OrganizationCreatedConsumer(PlatformDbContext db, IClock clock, LookupTableSeeder seeder)
    : IntegrationEventConsumer<OrganizationCreatedIntegrationEvent, PlatformDbContext>(db, clock)
{
    protected override Task ConsumeAsync(OrganizationCreatedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        seeder.SeedAsync(cancellationToken);
}
