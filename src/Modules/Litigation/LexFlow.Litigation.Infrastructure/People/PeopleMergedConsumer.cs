using LexFlow.Application.Abstractions;
using LexFlow.Crm.Contracts;
using LexFlow.Infrastructure.Common.Messaging;
using LexFlow.Litigation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Litigation.Infrastructure.People;

/// <summary>
/// No Worker: dois cadastros foram unificados no Crm, e as partes e o cliente pagador dos processos passam a apontar
/// para o cadastro mantido. Idempotente (inbox do módulo).
/// </summary>
public sealed class PeopleMergedConsumer(LitigationDbContext db, IClock clock)
    : IntegrationEventConsumer<PeopleMergedIntegrationEvent, LitigationDbContext>(db, clock)
{
    protected override async Task ConsumeAsync(PeopleMergedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        var merged = integrationEvent.MergedPersonId;
        var cases = await Db.Cases.Include(c => c.Parties)
            .Where(c => c.PayerPersonId == merged || c.Parties.Any(p => p.PersonId == merged))
            .ToListAsync(cancellationToken);
        foreach (var legalCase in cases)
        {
            legalCase.ReplacePerson(merged, integrationEvent.KeptPersonId);
        }
    }
}
