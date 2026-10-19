using FluentValidation;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Publications.Domain;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Publications.Application;

public sealed record CaptureSourceResponse(Guid Id, CaptureSourceKind Kind, string Label, string Value, bool IsActive)
{
    public static CaptureSourceResponse Of(CaptureSource source) => new(source.Id, source.Kind, source.Label, source.Value, source.IsActive);
}
public sealed record ListCaptureSourcesQuery : IQuery<IReadOnlyList<CaptureSourceResponse>>;
public sealed record GetCaptureSourceQuery(Guid Id) : IQuery<CaptureSourceResponse>;
public sealed record CreateCaptureSourceCommand(CaptureSourceInput Input) : ICommand<CaptureSourceResponse>;
public sealed record UpdateCaptureSourceCommand(Guid Id, CaptureSourceInput Input) : ICommand<CaptureSourceResponse>;

internal sealed class CreateSourceValidator : AbstractValidator<CreateCaptureSourceCommand>
{
    public CreateSourceValidator() => RuleFor(c => c.Input).NotNull();
}
internal sealed class UpdateSourceValidator : AbstractValidator<UpdateCaptureSourceCommand>
{
    public UpdateSourceValidator() => RuleFor(c => c.Input).NotNull();
}

internal sealed class ListCaptureSourcesHandler(IPublicationsDbContext db) : IQueryHandler<ListCaptureSourcesQuery, IReadOnlyList<CaptureSourceResponse>>
{
    public async Task<Result<IReadOnlyList<CaptureSourceResponse>>> Handle(ListCaptureSourcesQuery query, CancellationToken cancellationToken) =>
        await db.Sources.AsNoTracking().OrderBy(s => s.Label).Select(s => new CaptureSourceResponse(s.Id, s.Kind, s.Label, s.Value, s.IsActive)).ToListAsync(cancellationToken);
}
internal sealed class GetCaptureSourceHandler(IPublicationsDbContext db) : IQueryHandler<GetCaptureSourceQuery, CaptureSourceResponse>
{
    public async Task<Result<CaptureSourceResponse>> Handle(GetCaptureSourceQuery query, CancellationToken cancellationToken)
    {
        var source = await db.Sources.AsNoTracking().SingleOrDefaultAsync(s => s.Id == query.Id, cancellationToken);
        return source is null ? PublicationErrors.SourceNotFound : CaptureSourceResponse.Of(source);
    }
}
internal sealed class CreateCaptureSourceHandler(IPublicationsDbContext db) : ICommandHandler<CreateCaptureSourceCommand, CaptureSourceResponse>
{
    public async Task<Result<CaptureSourceResponse>> Handle(CreateCaptureSourceCommand command, CancellationToken cancellationToken)
    {
        var result = CaptureSource.Create(command.Input);
        if (result.IsFailure) { return result.Error; }
        db.Sources.Add(result.Value);
        await db.SaveChangesAsync(cancellationToken);
        return CaptureSourceResponse.Of(result.Value);
    }
}
internal sealed class UpdateCaptureSourceHandler(IPublicationsDbContext db) : ICommandHandler<UpdateCaptureSourceCommand, CaptureSourceResponse>
{
    public async Task<Result<CaptureSourceResponse>> Handle(UpdateCaptureSourceCommand command, CancellationToken cancellationToken)
    {
        var source = await db.Sources.SingleOrDefaultAsync(s => s.Id == command.Id, cancellationToken);
        if (source is null) { return PublicationErrors.SourceNotFound; }
        var result = source.Update(command.Input);
        if (result.IsFailure) { return result.Error; }
        await db.SaveChangesAsync(cancellationToken);
        return CaptureSourceResponse.Of(source);
    }
}
