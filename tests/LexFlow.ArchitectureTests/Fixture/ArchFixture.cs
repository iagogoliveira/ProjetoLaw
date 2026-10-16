// Fixture com violações plantadas, usado só por LayerRulesSelfTests para provar que as regras detectam erros.
// Fica fora do prefixo LexFlow, então não entra nas regras aplicadas ao código de produção.
#pragma warning disable CA1812, CA1822, MA0048

namespace ArchFixture.Billing.Contracts
{
    public sealed record InvoiceIssued(Guid InvoiceId);
}

namespace ArchFixture.Billing.Infrastructure
{
    public sealed class InvoiceRepository
    {
        public void Save(object invoice) => _ = invoice;
    }
}

namespace ArchFixture.Billing.Domain
{
    /// <summary>Violação: Domain usa Infrastructure do próprio módulo.</summary>
    public sealed class Invoice
    {
        public void Persist(ArchFixture.Billing.Infrastructure.InvoiceRepository repository) => repository.Save(this);
    }
}

namespace ArchFixture.Crm.Application
{
    /// <summary>Violação: Application de um módulo usa Domain de outro.</summary>
    public sealed class ChargeClientHandler
    {
        public ArchFixture.Billing.Domain.Invoice? Last { get; private set; }

        public void Handle(ArchFixture.Billing.Domain.Invoice invoice) => Last = invoice;
    }

    /// <summary>Permitido: consumir Contracts de outro módulo.</summary>
    public sealed class InvoiceIssuedConsumer
    {
        public Guid Handle(ArchFixture.Billing.Contracts.InvoiceIssued message) => message.InvoiceId;
    }
}
