using LexFlow.SharedKernel.Domain;

namespace LexFlow.Crm.Domain.Proposals;

/// <summary>
/// Modelo DOCX de proposta enviado pelo escritório (decisão de 2026-10-17: modelo padrão do sistema e upload do
/// próprio). O arquivo fica no armazenamento de objetos com o id do modelo; aqui ficam o nome e os dados de controle.
/// A biblioteca completa de modelos chega com o módulo de documentos (Etapa 9).
/// </summary>
public sealed class ProposalTemplate : AggregateRoot<Guid>, ITenantOwned, IAuditable, ISoftDeletable
{
    public const int MaxNameLength = 100;

    private ProposalTemplate()
    {
    }

    private ProposalTemplate(Guid id, string name, string fileName, long size, string sha256)
        : base(id)
    {
        Name = name;
        FileName = fileName;
        Size = size;
        Sha256 = sha256;
    }

    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string FileName { get; private set; } = string.Empty;

    public long Size { get; private set; }

    /// <summary>Hash SHA-256 (hexadecimal, minúsculas) do arquivo guardado.</summary>
    public string Sha256 { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public static ProposalTemplate Create(Guid id, string name, string fileName, long size, string sha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        return new ProposalTemplate(id, name.Trim(), fileName.Trim(), size, sha256);
    }
}
