namespace LexFlow.Litigation.Contracts;

/// <summary>
/// Casos sigilosos (muralha ética, seção 6.3) que o usuário da requisição não pode ver. Os módulos com dados ligados a
/// processo (<c>ICaseScoped</c>: documentos, prazos, horas, despesas) escondem o que pertence a esses casos.
/// Implementado pelo Litigation.
/// </summary>
public interface ICaseVisibility
{
    Task<IReadOnlyCollection<Guid>> HiddenCaseIdsAsync(CancellationToken cancellationToken);
}
