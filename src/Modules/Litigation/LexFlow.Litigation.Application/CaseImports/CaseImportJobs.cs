using System.Text.Json;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Crm.Contracts;
using LexFlow.Identity.Contracts;
using LexFlow.Litigation.Application.Abstractions;
using LexFlow.Litigation.Application.Cases;
using LexFlow.Litigation.Domain.CaseImports;
using LexFlow.Litigation.Domain.Cases;
using LexFlow.Organization.Contracts.OrgStructure;
using LexFlow.Platform.Contracts.Courts;
using LexFlow.Platform.Contracts.CustomFields;
using LexFlow.Platform.Contracts.Jobs;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Litigation.Application.CaseImports;

/// <summary>Uma linha da pré-visualização.</summary>
public sealed record ImportPreviewRow(int Row, CaseKind Kind, string? Cnj, string? Folder, string Client, string? Opponent);

/// <summary>Dependências de leitura dos cadastros do escritório usadas na validação das linhas.</summary>
internal sealed record ImportDirectories(IOrganizationDirectory Organization, ILookupItems Lookups, IUserDirectory Users, ICourtDirectory Courts);

/// <summary>
/// Os jobs da importação (Hangfire, no Worker, com o escritório de quem enviou): validar o arquivo para a
/// pré-visualização e, confirmada, gravar as linhas válidas em lotes de <see cref="BatchSize"/>. A gravação anota a
/// última linha de cada lote na mesma transação dele: uma nova tentativa continua dali, sem duplicar processos.
/// </summary>
internal sealed class CaseImportJobs
{
    public const int BatchSize = 500;
    public const int PreviewSize = 20;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ILitigationDbContext _db;
    private readonly ICaseSheets _sheets;
    private readonly IObjectStorage _storage;
    private readonly ITenantContext _tenant;
    private readonly IJobTracker _jobs;
    private readonly IPeopleRegistry _people;
    private readonly CaseFolders _folders;
    private readonly ImportDirectories _directories;
    private readonly IIdGenerator _ids;
    private readonly IClock _clock;

    public CaseImportJobs(
        ILitigationDbContext db,
        ICaseSheets sheets,
        IObjectStorage storage,
        ITenantContext tenant,
        IJobTracker jobs,
        IPeopleRegistry people,
        CaseFolders folders,
        ImportDirectories directories,
        IIdGenerator ids,
        IClock clock)
    {
        (_db, _sheets, _storage, _tenant, _jobs, _people, _folders, _directories, _ids, _clock) =
            (db, sheets, storage, tenant, jobs, people, folders, directories, ids, clock);
    }

    public static ObjectKey FileKey(Guid organizationId, Guid importId) => new(organizationId, "litigation-imports", importId);

    public async Task ValidateAsync(Guid importId, Guid jobId)
    {
        var cancellationToken = CancellationToken.None;
        await _jobs.StartAsync(jobId, cancellationToken);
        var import = await LoadAsync(importId, cancellationToken);
        if (import.Status != CaseImportStatus.Validating)
        {
            // Nova tentativa depois de concluída (ou já confirmada): nada a refazer.
            await _jobs.SucceedAsync(jobId, Location(importId), cancellationToken);
            return;
        }

        var sheet = await ReadAsync(import, jobId, cancellationToken);
        if (sheet is null)
        {
            return;
        }

        var (valid, errors) = await CheckAsync(import.Id, sheet.Rows, cancellationToken);
        await ReplaceErrorsAsync(import.Id, errors, cancellationToken);
        var preview = valid.Take(PreviewSize)
            .Select(r => new ImportPreviewRow(r.Number, r.Kind, CnjFormat.Of(r.CnjDigits), r.Folder, r.Client.Name, r.Opponent?.Name))
            .ToList();
        import.Validated(sheet.Rows.Count, errors.Select(e => e.Row).Distinct().Count(), JsonSerializer.Serialize(preview, Json));
        await _db.SaveChangesAsync(cancellationToken);
        await _jobs.SucceedAsync(jobId, Location(importId), cancellationToken);
    }

    public async Task ImportAsync(Guid importId, Guid jobId)
    {
        var cancellationToken = CancellationToken.None;
        await _jobs.StartAsync(jobId, cancellationToken);
        var import = await LoadAsync(importId, cancellationToken);
        if (import.Status != CaseImportStatus.Importing)
        {
            await _jobs.SucceedAsync(jobId, Location(importId), cancellationToken);
            return;
        }

        var sheet = await ReadAsync(import, jobId, cancellationToken);
        if (sheet is null)
        {
            return;
        }

        // Revalida as linhas que faltam (os cadastros podem ter mudado desde a pré-visualização).
        var resuming = import.LastImportedRow > 0;
        var pending = sheet.Rows.Where(r => r.Number > import.LastImportedRow).ToList();
        var (valid, errors) = await CheckAsync(import.Id, pending, cancellationToken);
        await (resuming ? AppendErrorsAsync(import.Id, errors, cancellationToken) : ReplaceErrorsAsync(import.Id, errors, cancellationToken));
        // Os erros desta revalidação ainda estão no ChangeTracker: uma consulta ao banco não os enxerga.
        var previousRows = resuming
            ? await _db.ImportErrors.Where(e => e.ImportId == import.Id).Select(e => e.Row).Distinct().ToListAsync(cancellationToken)
            : [];
        var invalidRows = previousRows.Concat(errors.Select(e => e.Row)).Distinct().Count();
        import.Revalidated(sheet.Rows.Count, invalidRows);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var batch in valid.Chunk(BatchSize))
        {
            await SaveBatchAsync(import.Id, batch, cancellationToken);
        }

        import = await LoadAsync(importId, cancellationToken);
        import.Complete(_clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
        await _jobs.SucceedAsync(jobId, Location(importId), cancellationToken);
    }

    private static string Location(Guid importId) => $"/api/v1/cases/imports/{importId:D}";

    private async Task<CaseImport> LoadAsync(Guid importId, CancellationToken cancellationToken) =>
        await _db.CaseImports.SingleOrDefaultAsync(i => i.Id == importId, cancellationToken)

        // O job pode começar antes de o pedido gravar a importação: a falha faz o Hangfire tentar de novo.
        ?? throw new InvalidOperationException($"Importação {importId} ainda não gravada.");

    private async Task<SheetData?> ReadAsync(CaseImport import, Guid jobId, CancellationToken cancellationToken)
    {
        await using var content = await _storage.OpenReadAsync(FileKey(_tenant.RequiredOrganizationId, import.Id), cancellationToken);
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;
        var sheet = _sheets.Read(buffer, import.FileName);
        if (sheet.IsSuccess)
        {
            return sheet.Value;
        }

        import.Fail(sheet.Error!.Code, _clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
        await _jobs.FailAsync(jobId, sheet.Error.Code, cancellationToken);
        return null;
    }

    private async Task<(List<ImportedRow> Valid, List<CaseImportError> Errors)> CheckAsync(Guid importId, IReadOnlyList<SheetRow> rows, CancellationToken cancellationToken)
    {
        var reader = await CaseRowReader.LoadAsync(_db, _directories.Organization, _directories.Lookups, _directories.Users, _directories.Courts, cancellationToken);
        var valid = new List<ImportedRow>();
        var errors = new List<CaseImportError>();
        foreach (var row in rows)
        {
            var (imported, cellErrors) = await reader.ReadAsync(row);
            if (imported is not null)
            {
                valid.Add(imported);
            }

            errors.AddRange(cellErrors.Select(e => new CaseImportError(importId, row.Number, e.Column, e.Value, e.Error)));
        }

        return (valid, errors);
    }

    private async Task ReplaceErrorsAsync(Guid importId, List<CaseImportError> errors, CancellationToken cancellationToken)
    {
        await _db.ImportErrors.Where(e => e.ImportId == importId).ExecuteDeleteAsync(cancellationToken);
        _db.ImportErrors.AddRange(errors);
    }

    private async Task AppendErrorsAsync(Guid importId, List<CaseImportError> errors, CancellationToken cancellationToken)
    {
        var known = (await _db.ImportErrors.Where(e => e.ImportId == importId).Select(e => e.Row).Distinct().ToListAsync(cancellationToken)).ToHashSet();
        _db.ImportErrors.AddRange(errors.Where(e => !known.Contains(e.Row)));
    }

    /// <summary>Um lote: as pessoas (Crm), os processos e o avanço da importação, gravados juntos no Litigation.</summary>
    private async Task SaveBatchAsync(Guid importId, ImportedRow[] batch, CancellationToken cancellationToken)
    {
        var seeds = batch.SelectMany(r => r.Opponent is null ? new[] { r.Client } : [r.Client, r.Opponent]).ToList();
        var personIds = await _people.EnsureAsync(seeds, cancellationToken);
        var next = 0;
        foreach (var row in batch)
        {
            var client = personIds[next++];
            Guid? opponent = row.Opponent is null ? null : personIds[next++];
            var folder = row.Folder ?? await _folders.NextAsync(row.PracticeAreaId, row.PracticeAreaName, cancellationToken);
            var legalCase = LegalCase.Create(_ids.NewId(), row.Kind, folder, Details(row, client, opponent));
            if (legalCase.IsSuccess)
            {
                _db.Cases.Add(legalCase.Value);
            }
            else
            {
                _db.ImportErrors.Add(new CaseImportError(importId, row.Number, ImportColumns.Kind, null, legalCase.Error!));
            }
        }

        var import = await LoadAsync(importId, cancellationToken);
        import.Progress(batch[^1].Number, batch.Length);
        await _db.SaveChangesAsync(cancellationToken);
        _db.ClearTracking();
    }

    private static CaseDetails Details(ImportedRow row, Guid client, Guid? opponent)
    {
        var parties = new List<PartyInput> { new(client, row.ClientPole, true, null, null) };
        if (opponent is { } other && other != client)
        {
            parties.Add(new PartyInput(other, row.ClientPole == PartyPole.Active ? PartyPole.Passive : PartyPole.Active, false, null, null));
        }

        IReadOnlyList<AssignmentInput> assignments = row.LawyerUserId is { } lawyer ? [new AssignmentInput(AssignmentRole.Lawyer, lawyer, null)] : [];
        return new CaseDetails(
            row.CnjDigits, row.CourtId, row.Instance, row.District, row.CourtUnit, row.Rite, row.ProceduralClass, row.Subject,
            row.ActionTypeId, row.PracticeAreaId, row.BranchId, row.PhaseId, row.ClaimValue, row.FiledOn, row.ServedOn,
            row.Description, null, parties, assignments);
    }
}
