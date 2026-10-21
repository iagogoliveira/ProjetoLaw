namespace LexFlow.Platform.Contracts.Notifications;

public sealed record DigestItem(string Kind, Guid Id, Guid? CaseId, string Label, DateTimeOffset At);
public sealed record DigestSection(string Kind, long Count, IReadOnlyList<DigestItem> Items);

/// <summary>Exportação opt-in por e-mail, com pertença e escopo atuais verificados pelo módulo dono; não simula sessão MFA.</summary>
public interface IDailyDigestSource
{
    Task<IReadOnlyList<DigestSection>> ReadAsync(Guid userId, DateOnly day, CancellationToken cancellationToken);
}
