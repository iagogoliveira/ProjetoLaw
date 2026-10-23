using LexFlow.SharedKernel.Domain;

namespace LexFlow.Scheduling.Domain.CalendarSync;

/// <summary>Estado externo simulado compartilhado entre API e Worker; nunca representa uma credencial OAuth.</summary>
public sealed class SimulatedCalendarItem : Entity<Guid>, ITenantOwned
{
    private SimulatedCalendarItem() { }
    public Guid OrganizationId { get; private set; }
    public Guid UserId { get; private set; }
    public CalendarProvider Provider { get; private set; }
    public string RemoteId { get; private set; } = string.Empty;
    public string? ClientKey { get; private set; }
    [Sensitive]
    public string Payload { get; private set; } = "null";
    public string? Link { get; private set; }

    public static SimulatedCalendarItem Create(Guid userId, CalendarProvider provider, string remoteId, string? clientKey, string payload, string? link) =>
        new() { Id = Guid.CreateVersion7(), UserId = userId, Provider = provider, RemoteId = remoteId, ClientKey = clientKey, Payload = payload, Link = link };
    public void Update(string payload, string link) { Payload = payload; Link = link; }
}
