namespace LexFlow.Organization.Contracts.OrgStructure;

/// <summary>Política do escritório ativo para a conferência dupla de prazos.</summary>
public interface IDeadlineReviewPolicy
{
    Task<bool> RequiredAsync(CancellationToken cancellationToken);
}
