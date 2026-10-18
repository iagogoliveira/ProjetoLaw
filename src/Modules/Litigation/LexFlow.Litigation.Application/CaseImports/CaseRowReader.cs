using System.Globalization;
using System.Text;
using LexFlow.Crm.Contracts;
using LexFlow.Identity.Contracts;
using LexFlow.Litigation.Application.Abstractions;
using LexFlow.Litigation.Domain;
using LexFlow.Litigation.Domain.Cases;
using LexFlow.Organization.Contracts.OrgStructure;
using LexFlow.Platform.Contracts.Courts;
using LexFlow.Platform.Contracts.CustomFields;
using LexFlow.SharedKernel.Results;
using LexFlow.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Litigation.Application.CaseImports;

/// <summary>Uma linha válida, já com os ids resolvidos, pronta para virar processo.</summary>
public sealed record ImportedRow(
    int Number,
    CaseKind Kind,
    string? CnjDigits,
    Guid? CourtId,
    CaseInstance? Instance,
    string? District,
    string? CourtUnit,
    string? Rite,
    string? ProceduralClass,
    string? Subject,
    Guid? ActionTypeId,
    Guid? PracticeAreaId,
    string? PracticeAreaName,
    Guid? BranchId,
    Guid? PhaseId,
    decimal? ClaimValue,
    DateOnly? FiledOn,
    DateOnly? ServedOn,
    string? Description,
    string? Folder,
    PersonSeed Client,
    PartyPole ClientPole,
    PersonSeed? Opponent,
    Guid? LawyerUserId);

/// <summary>Um problema numa célula: a coluna, o valor lido e o erro.</summary>
public sealed record CellError(string Column, string? Value, Error Error);

/// <summary>
/// Valida as linhas da importação contra os cadastros do escritório, carregados uma vez (áreas, filiais, fases, tipos
/// de ação, membros, CNJs e pastas já usados). Guarda os CNJs e pastas já vistos no arquivo: a repetição numa linha
/// seguinte é erro dela. Uma instância por leitura do arquivo.
/// </summary>
internal sealed class CaseRowReader
{
    public static readonly Error TextTooLong = Error.Validation("litigation.text_too_long", "Texto acima do tamanho permitido.");

    private static readonly string[] DateFormats = ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd"];

    private readonly Dictionary<string, OrgUnit> _areas;
    private readonly Dictionary<string, Guid> _branches;
    private readonly Dictionary<string, Guid> _phases;
    private readonly Dictionary<string, Guid> _actionTypes;
    private readonly Dictionary<string, Guid> _members;
    private readonly HashSet<string> _cnjs;
    private readonly HashSet<string> _folders;
    private readonly HashSet<string> _fileCnjs = new(StringComparer.Ordinal);
    private readonly HashSet<string> _fileFolders = new(StringComparer.Ordinal);
    private readonly Func<int, string, Task<Guid?>> _court;

    private CaseRowReader(
        Dictionary<string, OrgUnit> areas,
        Dictionary<string, Guid> branches,
        Dictionary<string, Guid> phases,
        Dictionary<string, Guid> actionTypes,
        Dictionary<string, Guid> members,
        HashSet<string> cnjs,
        HashSet<string> folders,
        Func<int, string, Task<Guid?>> court)
    {
        (_areas, _branches, _phases, _actionTypes, _members, _cnjs, _folders, _court) = (areas, branches, phases, actionTypes, members, cnjs, folders, court);
    }

    public static async Task<CaseRowReader> LoadAsync(
        ILitigationDbContext db,
        IOrganizationDirectory organization,
        ILookupItems lookups,
        IUserDirectory users,
        ICourtDirectory courts,
        CancellationToken cancellationToken)
    {
        var areas = (await organization.ListAsync(OrgUnitKind.PracticeArea, cancellationToken)).DistinctBy(a => Key(a.Name), StringComparer.Ordinal).ToDictionary(a => Key(a.Name), StringComparer.Ordinal);
        var branches = ByName(await organization.ListAsync(OrgUnitKind.Branch, cancellationToken), b => b.Name, b => b.Id);
        var phases = ByName(await lookups.ActiveItemsAsync(SystemLookupTables.CasePhases, cancellationToken), i => i.Name, i => i.Id);
        var actionTypes = ByName(await lookups.ActiveItemsAsync(SystemLookupTables.ActionTypes, cancellationToken), i => i.Name, i => i.Id);
        var memberIds = await organization.MemberUserIdsAsync(cancellationToken);
        var members = ByName(await users.GetManyAsync(memberIds, cancellationToken), u => u.Email, u => u.Id);
        var cnjs = (await db.Cases.Where(c => c.CnjNumber != null).Select(c => c.CnjNumber!).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        var folders = (await db.Cases.Select(c => c.InternalFolder).ToListAsync(cancellationToken)).Select(Key).ToHashSet(StringComparer.Ordinal);

        var courtCache = new Dictionary<(int, string), Guid?>();
        async Task<Guid?> CourtAsync(int segment, string code)
        {
            if (!courtCache.TryGetValue((segment, code), out var id))
            {
                id = (await courts.FindAsync(segment, code, cancellationToken))?.Id;
                courtCache[(segment, code)] = id;
            }

            return id;
        }

        return new CaseRowReader(areas, branches, phases, actionTypes, members, cnjs, folders, CourtAsync);
    }

    /// <summary>Texto para comparar nomes: sem acentos, minúsculo e com espaços simples.</summary>
    public static string Key(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var c in text.Trim().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                if (builder.Length > 0 && builder[^1] != ' ')
                {
                    builder.Append(' ');
                }

                continue;
            }

            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>Valida a linha; com erro em qualquer coluna, a linha inteira fica de fora (todos os erros são listados).</summary>
    public async Task<(ImportedRow? Row, IReadOnlyList<CellError> Errors)> ReadAsync(SheetRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var errors = new List<CellError>();
        var kind = ParseKind(row, errors);
        var (cnj, court) = await CnjAsync(row, kind, errors);
        var folder = Folder(row, errors);
        var texts = Texts(row, errors);
        var references = References(row, errors);
        var claimValue = Number(row, ImportColumns.ClaimValue, errors);
        var filedOn = Date(row, ImportColumns.FiledOn, errors);
        var servedOn = Date(row, ImportColumns.ServedOn, errors);
        var (client, pole, opponent) = People(row, errors);
        var instance = ParseInstance(row, errors);

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        // Só a linha válida reserva o CNJ e a pasta: uma repetição depois dela é que vira erro.
        if (cnj is not null)
        {
            _fileCnjs.Add(cnj);
        }

        if (folder is not null)
        {
            _fileFolders.Add(Key(folder));
        }

        return (new ImportedRow(
            row.Number, kind!.Value, cnj, court, instance, texts.District, texts.CourtUnit, texts.Rite, texts.ProceduralClass, texts.Subject,
            references.ActionType, references.Area?.Id, references.Area?.Name, references.Branch, references.Phase, claimValue, filedOn, servedOn,
            texts.Description, folder, client!, pole, opponent, references.Lawyer), errors);
    }

    private static Dictionary<string, Guid> ByName<T>(IEnumerable<T> items, Func<T, string> name, Func<T, Guid> id) =>
        items.DistinctBy(i => Key(name(i)), StringComparer.Ordinal).ToDictionary(i => Key(name(i)), id, StringComparer.Ordinal);

    private static CaseKind? ParseKind(SheetRow row, List<CellError> errors)
    {
        var value = row[ImportColumns.Kind];
        CaseKind? kind = Key(value) switch
        {
            "judicial" => CaseKind.Judicial,
            "administrativo" => CaseKind.Administrative,
            "extrajudicial" => CaseKind.Extrajudicial,
            _ => null,
        };
        if (kind is null)
        {
            errors.Add(new CellError(ImportColumns.Kind, value, LitigationErrors.ImportKindInvalid));
        }

        return kind;
    }

    private static CaseInstance? ParseInstance(SheetRow row, List<CellError> errors)
    {
        var value = row[ImportColumns.Instance];
        if (value is null)
        {
            return null;
        }

        CaseInstance? instance = Key(value) switch
        {
            "1" or "1a" or "1ª" or "1º" or "primeira" or "primeira instancia" => CaseInstance.First,
            "2" or "2a" or "2ª" or "2º" or "segunda" or "segunda instancia" => CaseInstance.Second,
            "superior" or "tribunal superior" => CaseInstance.Superior,
            "supremo" or "stf" => CaseInstance.Supreme,
            _ => null,
        };
        if (instance is null)
        {
            errors.Add(new CellError(ImportColumns.Instance, value, LitigationErrors.ImportInstanceInvalid));
        }

        return instance;
    }

    private async Task<(string? Cnj, Guid? Court)> CnjAsync(SheetRow row, CaseKind? kind, List<CellError> errors)
    {
        var value = row[ImportColumns.Cnj];
        if (kind is null)
        {
            return (null, null);
        }

        var cnj = LegalCase.NormalizeCnj(kind.Value, value);
        if (cnj.IsFailure)
        {
            errors.Add(new CellError(ImportColumns.Cnj, value, cnj.Error!));
            return (null, null);
        }

        if (cnj.Value is not { } digits)
        {
            return (null, null);
        }

        if (_cnjs.Contains(digits) || _fileCnjs.Contains(digits))
        {
            errors.Add(new CellError(ImportColumns.Cnj, value, _cnjs.Contains(digits) ? LitigationErrors.CnjTaken : LitigationErrors.ImportDuplicateInFile));
            return (null, null);
        }

        var number = CnjNumber.Create(digits).Value;
        return (digits, await _court(number.Segment, number.Court));
    }

    private string? Folder(SheetRow row, List<CellError> errors)
    {
        var value = row[ImportColumns.Folder];
        if (value is null)
        {
            return null;
        }

        if (value.Length > 80)
        {
            errors.Add(new CellError(ImportColumns.Folder, value, TextTooLong));
            return null;
        }

        if (_folders.Contains(Key(value)) || _fileFolders.Contains(Key(value)))
        {
            errors.Add(new CellError(ImportColumns.Folder, value, _folders.Contains(Key(value)) ? LitigationErrors.FolderTaken : LitigationErrors.ImportDuplicateInFile));
            return null;
        }

        return value;
    }

    private static (string? District, string? CourtUnit, string? Rite, string? ProceduralClass, string? Subject, string? Description) Texts(SheetRow row, List<CellError> errors)
    {
        string? Text(string column, int max)
        {
            var value = row[column];
            if (value is { Length: var length } && length > max)
            {
                errors.Add(new CellError(column, value, TextTooLong));
                return null;
            }

            return value;
        }

        return (
            Text(ImportColumns.District, LegalCase.MaxTextLength),
            Text(ImportColumns.CourtUnit, LegalCase.MaxTextLength),
            Text(ImportColumns.Rite, LegalCase.MaxTextLength),
            Text(ImportColumns.ProceduralClass, LegalCase.MaxTextLength),
            Text(ImportColumns.Subject, LegalCase.MaxTextLength),
            Text(ImportColumns.Description, LegalCase.MaxDescriptionLength));
    }

    private (Guid? ActionType, OrgUnit? Area, Guid? Branch, Guid? Phase, Guid? Lawyer) References(SheetRow row, List<CellError> errors)
    {
        Guid? Find(string column, Dictionary<string, Guid> items, Error notFound)
        {
            var value = row[column];
            if (value is null)
            {
                return null;
            }

            if (items.TryGetValue(Key(value), out var id))
            {
                return id;
            }

            errors.Add(new CellError(column, value, notFound));
            return null;
        }

        OrgUnit? area = null;
        if (row[ImportColumns.PracticeArea] is { } areaName && !_areas.TryGetValue(Key(areaName), out area))
        {
            errors.Add(new CellError(ImportColumns.PracticeArea, areaName, LitigationErrors.PracticeAreaNotFound));
        }

        return (
            Find(ImportColumns.ActionType, _actionTypes, LitigationErrors.ActionTypeNotFound),
            area,
            Find(ImportColumns.Branch, _branches, LitigationErrors.BranchNotFound),
            Find(ImportColumns.Phase, _phases, LitigationErrors.PhaseNotFound),
            Find(ImportColumns.LawyerEmail, _members, LitigationErrors.ResponsibleNotMember));
    }

    private static (PersonSeed? Client, PartyPole Pole, PersonSeed? Opponent) People(SheetRow row, List<CellError> errors)
    {
        var clientName = row[ImportColumns.ClientName];
        if (clientName is null)
        {
            errors.Add(new CellError(ImportColumns.ClientName, null, LitigationErrors.ImportClientRequired));
        }
        else if (clientName.Length > 200)
        {
            errors.Add(new CellError(ImportColumns.ClientName, clientName, TextTooLong));
        }

        var clientDocument = Document(row, ImportColumns.ClientDocument, errors);
        var poleValue = row[ImportColumns.ClientPole];
        var pole = Key(poleValue) switch
        {
            "" or "ativo" or "autor" => PartyPole.Active,
            "passivo" or "reu" => PartyPole.Passive,
            _ => (PartyPole?)null,
        };
        if (pole is null)
        {
            errors.Add(new CellError(ImportColumns.ClientPole, poleValue, Error.Validation("litigation.pole_invalid", "Polo inválido: use ativo ou passivo.")));
        }

        var opponentName = row[ImportColumns.OpponentName];
        var opponentDocument = Document(row, ImportColumns.OpponentDocument, errors);
        if (opponentName is { Length: > 200 })
        {
            errors.Add(new CellError(ImportColumns.OpponentName, opponentName, TextTooLong));
        }

        if (clientDocument is not null && string.Equals(clientDocument, opponentDocument, StringComparison.Ordinal))
        {
            errors.Add(new CellError(ImportColumns.OpponentDocument, row[ImportColumns.OpponentDocument], LitigationErrors.PartyDuplicated));
        }

        var client = clientName is null ? null : new PersonSeed(clientName, clientDocument, PersonSeedRole.Client);
        var opponent = opponentName is null ? null : new PersonSeed(opponentName, opponentDocument, PersonSeedRole.Opponent);
        return (client, pole ?? PartyPole.Active, opponent);
    }

    private static string? Document(SheetRow row, string column, List<CellError> errors)
    {
        var value = row[column];
        if (value is null)
        {
            return null;
        }

        if (Cpf.Create(value) is { IsSuccess: true } cpf)
        {
            return cpf.Value.Value;
        }

        if (Cnpj.Create(value) is { IsSuccess: true } cnpj)
        {
            return cnpj.Value.Value;
        }

        errors.Add(new CellError(column, value, LitigationErrors.ImportDocumentInvalid));
        return null;
    }

    /// <summary>Número em reais: <c>1.234,56</c>, <c>1234.56</c> ou o número da célula; não pode ser negativo.</summary>
    private static decimal? Number(SheetRow row, string column, List<CellError> errors)
    {
        var value = row[column];
        if (value is null)
        {
            return null;
        }

        var text = value.Replace("R$", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal);
        if (text.Contains(',', StringComparison.Ordinal))
        {
            text = text.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.');
        }

        if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number))
        {
            errors.Add(new CellError(column, value, LitigationErrors.ImportNumberInvalid));
            return null;
        }

        if (number is < 0 or >= 1_000_000_000_000_000m)
        {
            errors.Add(new CellError(column, value, LitigationErrors.ValueInvalid));
            return null;
        }

        return decimal.Round(number, 2, MidpointRounding.AwayFromZero);
    }

    private static DateOnly? Date(SheetRow row, string column, List<CellError> errors)
    {
        var value = row[column];
        if (value is null)
        {
            return null;
        }

        if (DateOnly.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        errors.Add(new CellError(column, value, LitigationErrors.ImportDateInvalid));
        return null;
    }
}
