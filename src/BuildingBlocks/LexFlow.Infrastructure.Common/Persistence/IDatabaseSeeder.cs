namespace LexFlow.Infrastructure.Common.Persistence;

/// <summary>
/// Dados que dependem do ambiente e não cabem numa migration (ex.: endereços de clientes OIDC). Rodado pelo
/// MigrationService depois de todas as migrations; precisa ser idempotente (cria ou atualiza).
/// </summary>
public interface IDatabaseSeeder
{
    Task SeedAsync(CancellationToken cancellationToken);
}
