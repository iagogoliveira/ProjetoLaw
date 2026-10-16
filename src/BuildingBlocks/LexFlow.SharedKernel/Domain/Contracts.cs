namespace LexFlow.SharedKernel.Domain;

/// <summary>Evento de domínio: ocorre dentro do módulo e é tratado na mesma transação.</summary>
#pragma warning disable CA1040 // interface marcadora: o despacho é por tipo
public interface IDomainEvent;
#pragma warning restore CA1040

/// <summary>Entidade que acumula eventos de domínio (implementada por <see cref="AggregateRoot{TId}"/>).</summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}

/// <summary>Dado de escritório: isolado por organização (filtro global, interceptor e, na Etapa 3, RLS).</summary>
public interface ITenantOwned
{
    Guid OrganizationId { get; }
}

/// <summary>Dado ligado a um processo/caso: sujeito à restrição de casos sigilosos (seção 6.3).</summary>
public interface ICaseScoped
{
    Guid? CaseId { get; }
}

/// <summary>Exclusão lógica: filtrada por padrão nas consultas. Os valores são preenchidos pelo interceptor.</summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; }

    DateTimeOffset? DeletedAt { get; }
}

/// <summary>Colunas de criação/alteração preenchidas pelo interceptor a partir de <c>IClock</c> e <c>ICurrentUser</c>.</summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; }

    Guid CreatedBy { get; }

    DateTimeOffset? UpdatedAt { get; }

    Guid? UpdatedBy { get; }
}

/// <summary>A propriedade nunca entra no campo <c>changes</c> da auditoria (ex.: textos longos de documentos).</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NotAuditedAttribute : Attribute;

/// <summary>Dado sensível (CPF, dados bancários, senhas): fora da auditoria e mascarado nos logs.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SensitiveAttribute : Attribute;
