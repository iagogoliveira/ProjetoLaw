using System.Security.Cryptography;
using FluentValidation;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Crm.Application.Abstractions;
using LexFlow.Crm.Domain;
using LexFlow.Crm.Domain.Opportunities;
using LexFlow.Crm.Domain.Proposals;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Crm.Application.Proposals;

/// <summary>Arquivo gerado ou guardado, para download.</summary>
public sealed record ProposalFile(byte[] Content, string FileName)
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
}

/// <summary>
/// Monta o DOCX de uma proposta: o modelo escolhido (do armazenamento) ou o padrão, com os dados da oportunidade e da
/// proposta. Arquivos no armazenamento usam o módulo <c>crm</c> e o id do modelo ou do documento congelado.
/// </summary>
internal sealed class ProposalDocumentComposer(ICrmDbContext db, IObjectStorage storage, IProposalDocuments documents, ITenantContext tenant, IClock clock)
{
    public const string StorageModule = "crm";

    public ObjectKey KeyOf(Guid id) => new(tenant.RequiredOrganizationId, StorageModule, id);

    public async Task<byte[]> RenderAsync(Proposal proposal, Opportunity opportunity, CancellationToken cancellationToken)
    {
        var data = new ProposalDocumentData(
            opportunity.ProspectName,
            opportunity.ProspectDocument,
            opportunity.ProspectEmail,
            opportunity.Title,
            proposal.Title,
            proposal.Scope,
            proposal.Conditions,
            clock.Today,
            proposal.ValidUntil,
            proposal.Total,
            [.. proposal.Items.OrderBy(i => i.Position).Select(i => new ProposalDocumentItem(i.Description, i.Kind, i.Quantity, i.UnitAmount, i.Percentage, i.Amount))]);

        // Modelo excluído depois de escolhido: a proposta sai no modelo padrão.
        if (proposal.TemplateId is { } templateId && await db.ProposalTemplates.AnyAsync(t => t.Id == templateId, cancellationToken))
        {
            await using var template = await storage.OpenReadAsync(KeyOf(templateId), cancellationToken);
            using var buffer = new MemoryStream();
            await template.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            return documents.Render(buffer, data);
        }

        return documents.Render(null, data);
    }

    public static string FileNameOf(Proposal proposal)
    {
        var safe = new string([.. proposal.Title.Select(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_' ? c : '_')]).Trim();
        return $"{(safe.Length == 0 ? "proposta" : safe[..Math.Min(safe.Length, 80)])}.docx";
    }
}

/// <summary>
/// O DOCX da proposta: o congelado no envio, quando já enviada; senão, gerado na hora com o modelo e os dados atuais.
/// </summary>
public sealed record GetProposalDocumentQuery(Guid Id) : IQuery<ProposalFile>;

internal sealed class GetProposalDocumentHandler(ICrmDbContext db, IObjectStorage storage, ProposalDocumentComposer composer)
    : IQueryHandler<GetProposalDocumentQuery, ProposalFile>
{
    public async Task<Result<ProposalFile>> Handle(GetProposalDocumentQuery query, CancellationToken cancellationToken)
    {
        var proposal = await db.Proposals.AsNoTracking().Include(p => p.Items).SingleOrDefaultAsync(p => p.Id == query.Id, cancellationToken);
        if (proposal is null)
        {
            return CrmErrors.ProposalNotFound;
        }

        if (proposal.DocumentId is { } documentId)
        {
            await using var stored = await storage.OpenReadAsync(composer.KeyOf(documentId), cancellationToken);
            using var buffer = new MemoryStream();
            await stored.CopyToAsync(buffer, cancellationToken);
            return new ProposalFile(buffer.ToArray(), ProposalDocumentComposer.FileNameOf(proposal));
        }

        var opportunity = await db.Opportunities.AsNoTracking().SingleAsync(o => o.Id == proposal.OpportunityId, cancellationToken);
        return new ProposalFile(await composer.RenderAsync(proposal, opportunity, cancellationToken), ProposalDocumentComposer.FileNameOf(proposal));
    }
}

/// <summary>
/// Registra o envio da proposta aprovada ao cliente: o DOCX é gerado e congelado no armazenamento, e a oportunidade em
/// lead ou reunião passa para a etapa proposta.
/// </summary>
public sealed record SendProposalCommand(Guid Id) : ICommand<ProposalResponse>;

internal sealed class SendProposalHandler(ICrmDbContext db, IObjectStorage storage, ProposalDocumentComposer composer, IIdGenerator ids, IClock clock)
    : ICommandHandler<SendProposalCommand, ProposalResponse>
{
    public async Task<Result<ProposalResponse>> Handle(SendProposalCommand command, CancellationToken cancellationToken)
    {
        var proposal = await db.Proposals.Include(p => p.Items).SingleOrDefaultAsync(p => p.Id == command.Id, cancellationToken);
        if (proposal is null)
        {
            return CrmErrors.ProposalNotFound;
        }

        var opportunity = await db.Opportunities.SingleAsync(o => o.Id == proposal.OpportunityId, cancellationToken);
        if (opportunity.IsClosed)
        {
            return opportunity.Stage == OpportunityStage.Won ? CrmErrors.OpportunityWon : CrmErrors.OpportunityLost;
        }

        var documentId = ids.NewId();
        var sent = proposal.Send(documentId, clock.Today, clock.UtcNow);
        if (sent.IsFailure)
        {
            return sent.Error;
        }

        var content = await composer.RenderAsync(proposal, opportunity, cancellationToken);
        using (var stream = new MemoryStream(content))
        {
            await storage.PutAsync(composer.KeyOf(documentId), stream, ProposalFile.ContentType, cancellationToken);
        }

        opportunity.ProposalSent(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return proposal.ToResponse();
    }
}

public sealed record ProposalTemplateResponse(Guid Id, string Name, string FileName, long Size, DateTimeOffset CreatedAt);

/// <summary>Modelos do escritório e os marcadores aceitos (para a tela mostrar como montar um modelo).</summary>
public sealed record ProposalTemplatesResponse(IReadOnlyList<ProposalTemplateResponse> Templates, IReadOnlyList<string> Markers);

public sealed record ListProposalTemplatesQuery : IQuery<ProposalTemplatesResponse>;

internal sealed class ListProposalTemplatesHandler(ICrmDbContext db, IProposalDocuments documents) : IQueryHandler<ListProposalTemplatesQuery, ProposalTemplatesResponse>
{
    public async Task<Result<ProposalTemplatesResponse>> Handle(ListProposalTemplatesQuery query, CancellationToken cancellationToken)
    {
        var templates = await db.ProposalTemplates.AsNoTracking().OrderBy(t => t.Name)
            .Select(t => new ProposalTemplateResponse(t.Id, t.Name, t.FileName, t.Size, t.CreatedAt))
            .ToListAsync(cancellationToken);
        return new ProposalTemplatesResponse(templates, documents.Markers);
    }
}

/// <summary>O modelo padrão do sistema, para o escritório baixar e adaptar.</summary>
public sealed record GetDefaultProposalTemplateQuery : IQuery<ProposalFile>;

internal sealed class GetDefaultProposalTemplateHandler(IProposalDocuments documents) : IQueryHandler<GetDefaultProposalTemplateQuery, ProposalFile>
{
    public Task<Result<ProposalFile>> Handle(GetDefaultProposalTemplateQuery query, CancellationToken cancellationToken) =>
        Task.FromResult<Result<ProposalFile>>(new ProposalFile(documents.DefaultTemplate(), "modelo-proposta-lexflow.docx"));
}

/// <summary>
/// Envio de um modelo DOCX pelo escritório: passa pelo antivírus antes de ser guardado, precisa abrir como DOCX e só
/// pode usar marcadores conhecidos (decisão de 2026-10-17).
/// </summary>
public sealed record UploadProposalTemplateCommand(string Name, string FileName, byte[] Content) : ICommand<ProposalTemplateResponse>;

internal sealed class UploadProposalTemplateValidator : AbstractValidator<UploadProposalTemplateCommand>
{
    /// <summary>Limite do modelo (5 MB): é só texto e formatação; imagens grandes não cabem num modelo de proposta.</summary>
    public const int MaxSize = 5 * 1024 * 1024;

    public UploadProposalTemplateValidator()
    {
        RuleFor(c => c.Name).NotEmpty().WithErrorCode("crm.name_required")
            .MaximumLength(ProposalTemplate.MaxNameLength).WithErrorCode("crm.name_too_long");
        RuleFor(c => c.FileName).Must(f => f.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)).WithErrorCode(CrmErrors.TemplateInvalid.Code);
        RuleFor(c => c.Content).NotEmpty().WithErrorCode(CrmErrors.TemplateInvalid.Code)
            .Must(c => c.Length <= MaxSize).WithErrorCode(CrmErrors.TemplateTooLarge.Code);
    }
}

internal sealed class UploadProposalTemplateHandler(
    ICrmDbContext db,
    IFileScanner scanner,
    IObjectStorage storage,
    IProposalDocuments documents,
    ProposalDocumentComposer composer,
    IIdGenerator ids) : ICommandHandler<UploadProposalTemplateCommand, ProposalTemplateResponse>
{
    public async Task<Result<ProposalTemplateResponse>> Handle(UploadProposalTemplateCommand command, CancellationToken cancellationToken)
    {
        using (var scanned = new MemoryStream(command.Content, writable: false))
        {
            if ((await scanner.ScanAsync(scanned, cancellationToken)).Status != FileScanStatus.Clean)
            {
                return CrmErrors.TemplateInfected;
            }
        }

        using (var checkedTemplate = new MemoryStream(command.Content, writable: false))
        {
            var valid = documents.Validate(checkedTemplate);
            if (valid.IsFailure)
            {
                return valid.Error;
            }
        }

        var fileName = Path.GetFileName(command.FileName);
        var template = ProposalTemplate.Create(ids.NewId(), command.Name, fileName, command.Content.Length, Convert.ToHexStringLower(SHA256.HashData(command.Content)));
        using (var content = new MemoryStream(command.Content, writable: false))
        {
            await storage.PutAsync(composer.KeyOf(template.Id), content, ProposalFile.ContentType, cancellationToken);
        }

        db.ProposalTemplates.Add(template);
        await db.SaveChangesAsync(cancellationToken);
        return new ProposalTemplateResponse(template.Id, template.Name, template.FileName, template.Size, template.CreatedAt);
    }
}

/// <summary>Exclui (logicamente) um modelo; propostas que o usavam passam a sair no modelo padrão.</summary>
public sealed record DeleteProposalTemplateCommand(Guid Id) : ICommand;

internal sealed class DeleteProposalTemplateHandler(ICrmDbContext db) : ICommandHandler<DeleteProposalTemplateCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteProposalTemplateCommand command, CancellationToken cancellationToken)
    {
        var template = await db.ProposalTemplates.SingleOrDefaultAsync(t => t.Id == command.Id, cancellationToken);
        if (template is null)
        {
            return CrmErrors.TemplateNotFound;
        }

        db.ProposalTemplates.Remove(template);
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
