namespace LexFlow.Application.Abstractions;

/// <summary>Relógio da aplicação. Nunca use <c>DateTime.Now</c> (proibido no build, ADR 0006).</summary>
public interface IClock
{
    /// <summary>Instante atual em UTC.</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>Data de hoje no fuso legal (<see cref="LegalTimeZone"/>), usada em prazos processuais.</summary>
    DateOnly Today { get; }

    /// <summary>America/Sao_Paulo (seção 4.4).</summary>
    TimeZoneInfo LegalTimeZone { get; }
}

/// <summary>
/// <see cref="IClock"/> sobre <see cref="TimeProvider"/>: <c>TimeProvider.System</c> em produção e
/// <c>FakeTimeProvider</c> nos testes (o "FakeClock" da seção 13.3).
/// </summary>
public sealed class TimeProviderClock(TimeProvider timeProvider) : IClock
{
    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(UtcNow, SaoPaulo).DateTime);

    public TimeZoneInfo LegalTimeZone => SaoPaulo;
}

/// <summary>Gerador de identificadores. IDs são UUID v7, ordenáveis por tempo (seção 4.4).</summary>
public interface IIdGenerator
{
    Guid NewId();
}

/// <summary>UUID v7 com o instante do <see cref="IClock"/>, para que IDs sigam o relógio também nos testes.</summary>
public sealed class GuidV7IdGenerator(IClock clock) : IIdGenerator
{
    public Guid NewId() => Guid.CreateVersion7(clock.UtcNow);
}

/// <summary>Usuário da requisição ou da mensagem em processamento.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Id do usuário (claim <c>sub</c>); nulo para anônimos e processos de sistema.</summary>
    Guid? UserId { get; }
}

/// <summary>
/// Organização (tenant) ativa. Na requisição vem do token (claim <c>org_id</c>); em jobs e consumidores, da mensagem.
/// A validação de pertença do usuário à organização chega na Etapa 3.
/// </summary>
public interface ITenantContext
{
    Guid? OrganizationId { get; }

    bool HasTenant => OrganizationId.HasValue;

    /// <summary>Organização ativa; lança se não houver (erro de programação: endpoint de escritório sem tenant).</summary>
    Guid RequiredOrganizationId => OrganizationId ?? throw new InvalidOperationException("Nenhuma organização ativa no contexto.");
}

/// <summary>
/// Dados de origem da operação atual (requisição HTTP ou mensagem), usados pela auditoria e pelos eventos de integração.
/// </summary>
public interface IRequestContext
{
    /// <summary>Id de correlação propagado entre API, Worker e mensagens.</summary>
    string? CorrelationId { get; }

    string? IpAddress { get; }

    string? UserAgent { get; }
}

/// <summary>
/// Tenant definido pelo próprio caso de uso, por escopo de DI. Único uso permitido: gravar os primeiros dados de uma
/// organização <b>recém-criada</b> no mesmo comando (onboarding), quando ainda não existe tenant ativo. As
/// implementações de <see cref="ITenantContext"/> dão precedência a ele.
/// </summary>
public sealed class TenantOverride
{
    public Guid? OrganizationId { get; private set; }

    public IDisposable BeginForNewOrganization(Guid organizationId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organização obrigatória.", nameof(organizationId));
        }

        if (OrganizationId is not null)
        {
            throw new InvalidOperationException("Já existe um tenant definido neste escopo.");
        }

        OrganizationId = organizationId;
        return new End(this);
    }

    private sealed class End(TenantOverride owner) : IDisposable
    {
        public void Dispose() => owner.OrganizationId = null;
    }
}
