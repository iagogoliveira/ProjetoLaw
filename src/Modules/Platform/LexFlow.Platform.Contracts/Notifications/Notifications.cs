namespace LexFlow.Platform.Contracts.Notifications;

public sealed record NotificationViewer(Guid UserId, bool MultiFactorAuthenticated);
public sealed record NotificationCandidate(Guid Id, string Kind, Guid ResourceId);
/// <summary>Autorização atual dos recursos de cada módulo; tipo desconhecido nunca é exposto.</summary>
public interface INotificationVisibility
{
    Task<IReadOnlyCollection<Guid>> VisibleAsync(NotificationViewer viewer, IReadOnlyList<NotificationCandidate> candidates, CancellationToken cancellationToken);
}
