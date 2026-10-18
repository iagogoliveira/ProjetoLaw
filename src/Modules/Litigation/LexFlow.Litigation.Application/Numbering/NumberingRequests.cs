using FluentValidation;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Litigation.Application.Abstractions;
using LexFlow.Litigation.Domain;
using LexFlow.Litigation.Domain.Numbering;
using LexFlow.Organization.Contracts.OrgStructure;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Litigation.Application.Numbering;

/// <summary>Formato da numeração e as siglas das áreas (as configuradas), com um exemplo do próximo número.</summary>
public sealed record NumberingResponse(string Format, IReadOnlyDictionary<Guid, string> AreaCodes, string Example);

public sealed record GetNumberingQuery : IQuery<NumberingResponse>;

internal sealed class GetNumberingHandler(ILitigationDbContext db, IClock clock) : IQueryHandler<GetNumberingQuery, NumberingResponse>
{
    public async Task<Result<NumberingResponse>> Handle(GetNumberingQuery query, CancellationToken cancellationToken)
    {
        var numbering = await db.Numberings.AsNoTracking().FirstOrDefaultAsync(cancellationToken) ?? CaseNumbering.Default(Guid.Empty);
        return new NumberingResponse(numbering.Format, numbering.Codes, numbering.Render("TRAB", clock.Today.Year, 1));
    }
}

/// <summary>Troca o formato e as siglas das áreas; os processos já numerados ficam como estão.</summary>
public sealed record ConfigureNumberingCommand(string Format, IReadOnlyDictionary<Guid, string>? AreaCodes) : ICommand<NumberingResponse>;

internal sealed class ConfigureNumberingValidator : AbstractValidator<ConfigureNumberingCommand>
{
    public ConfigureNumberingValidator() =>
        RuleFor(c => c.Format).NotEmpty().WithErrorCode(LitigationErrors.NumberingFormatInvalid.Code)
            .MaximumLength(CaseNumbering.MaxFormatLength).WithErrorCode(LitigationErrors.NumberingFormatInvalid.Code);
}

internal sealed class ConfigureNumberingHandler(ILitigationDbContext db, IOrganizationDirectory organization, IIdGenerator ids, IClock clock)
    : ICommandHandler<ConfigureNumberingCommand, NumberingResponse>
{
    public async Task<Result<NumberingResponse>> Handle(ConfigureNumberingCommand command, CancellationToken cancellationToken)
    {
        var codes = command.AreaCodes ?? new Dictionary<Guid, string>();
        if (codes.Count > 0 && (await organization.GetAsync(OrgUnitKind.PracticeArea, [.. codes.Keys], cancellationToken)).Count != codes.Count)
        {
            return LitigationErrors.PracticeAreaNotFound;
        }

        var numbering = await db.Numberings.FirstOrDefaultAsync(cancellationToken);
        if (numbering is null)
        {
            numbering = CaseNumbering.Default(ids.NewId());
            db.Numberings.Add(numbering);
        }

        var configured = numbering.Configure(command.Format, codes);
        if (configured.IsFailure)
        {
            return configured.Error;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new NumberingResponse(numbering.Format, numbering.Codes, numbering.Render("TRAB", clock.Today.Year, 1));
    }
}
