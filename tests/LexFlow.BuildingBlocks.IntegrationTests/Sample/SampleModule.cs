using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Infrastructure.Common.Persistence;
using LexFlow.Sample.Domain;
using LexFlow.Sample.Infrastructure;
using LexFlow.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// Módulo de exemplo (LexFlow.Sample.*) usado só nos testes da base de persistência.
namespace LexFlow.Sample.Domain
{
    public sealed record MatterOpened(Guid MatterId) : IDomainEvent;

    /// <summary>Entidade de escritório típica: tenant, auditoria, exclusão lógica e um campo sensível.</summary>
    public sealed class Matter : AggregateRoot<Guid>, ITenantOwned, IAuditable, ISoftDeletable
    {
        private Matter()
        {
        }

        public Matter(Guid id, string title, string? clientCpf, Guid organizationId = default)
            : base(id)
        {
            Title = title;
            ClientCpf = clientCpf;
            OrganizationId = organizationId;
            Raise(new MatterOpened(id));
        }

        public Guid OrganizationId { get; private set; }

        public string Title { get; private set; } = string.Empty;

        [Sensitive]
        public string? ClientCpf { get; private set; }

        [NotAudited]
        public string? Notes { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public Guid CreatedBy { get; private set; }

        public DateTimeOffset? UpdatedAt { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public bool IsDeleted { get; private set; }

        public DateTimeOffset? DeletedAt { get; private set; }

        public void Rename(string title) => Title = title;

        public void ChangeCpf(string cpf) => ClientCpf = cpf;

        public void Annotate(string notes) => Notes = notes;
    }

    /// <summary>Criada pelo handler do evento de domínio, no mesmo SaveChanges.</summary>
    public sealed class MatterTimelineEntry : Entity<Guid>, ITenantOwned
    {
        private MatterTimelineEntry()
        {
        }

        public MatterTimelineEntry(Guid id, Guid matterId, string text)
            : base(id)
        {
            MatterId = matterId;
            Text = text;
        }

        public Guid OrganizationId { get; private set; }

        public Guid MatterId { get; private set; }

        public string Text { get; private set; } = string.Empty;
    }
}

namespace LexFlow.Sample.Application
{
    internal sealed class MatterOpenedHandler(SampleDbContext db, IIdGenerator ids) : IDomainEventHandler<MatterOpened>
    {
        public Task Handle(MatterOpened domainEvent, CancellationToken cancellationToken)
        {
            db.Timeline.Add(new MatterTimelineEntry(ids.NewId(), domainEvent.MatterId, "Processo aberto"));
            return Task.CompletedTask;
        }
    }
}

namespace LexFlow.Sample.Infrastructure
{
    public sealed class SampleDbContext(DbContextOptions<SampleDbContext> options, ITenantContext tenant)
        : ModuleDbContextBase(options, tenant)
    {
        public DbSet<Matter> Matters => Set<Matter>();

        public DbSet<MatterTimelineEntry> Timeline => Set<MatterTimelineEntry>();

        protected override string Schema => "sample";

        /// <summary>Nos testes este contexto cria a tabela de auditoria (em produção é o Platform).</summary>
        protected override bool OwnsAuditLog => true;
    }

    internal sealed class MatterConfiguration : IEntityTypeConfiguration<Matter>
    {
        public void Configure(EntityTypeBuilder<Matter> builder)
        {
            builder.HasKey(m => m.Id);
            builder.Property(m => m.Title).HasMaxLength(300);
            builder.HasIndex(m => new { m.OrganizationId, m.Title });
            builder.HasStandardColumns();
        }
    }
}
