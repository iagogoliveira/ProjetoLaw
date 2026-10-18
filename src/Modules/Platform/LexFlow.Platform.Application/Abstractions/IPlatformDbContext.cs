using LexFlow.Platform.Domain.Calendar;
using LexFlow.Platform.Domain.Courts;
using LexFlow.Platform.Domain.CustomFields;
using LexFlow.Platform.Domain.Idempotency;
using LexFlow.Platform.Domain.Indexes;
using LexFlow.Platform.Domain.Jobs;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Platform.Application.Abstractions;

/// <summary>Acesso aos dados do módulo pelos handlers (implementado pelo <c>PlatformDbContext</c> na Infrastructure).</summary>
public interface IPlatformDbContext
{
    DbSet<Job> Jobs { get; }

    DbSet<IdempotencyKey> IdempotencyKeys { get; }

    DbSet<Holiday> Holidays { get; }

    DbSet<Court> Courts { get; }

    DbSet<CustomFieldDefinition> CustomFields { get; }

    DbSet<LookupTable> LookupTables { get; }

    DbSet<LookupItem> LookupItems { get; }

    DbSet<MonetaryIndex> MonetaryIndexes { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
