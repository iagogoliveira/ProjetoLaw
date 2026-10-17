using FluentValidation;
using LexFlow.Application.Abstractions.Data;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Application.Abstractions.Security;
using LexFlow.SharedKernel.Results;

// Namespace no formato LexFlow.{Modulo}.Application para exercitar a descoberta do módulo pelo decorator de transação.
namespace LexFlow.Sample.Application;

/// <summary>Registro da ordem em que pipeline, handler e transação são executados.</summary>
public sealed class Trace
{
    public List<string> Steps { get; } = [];
}

public sealed record CreateThing(string Title, bool Fail = false, bool Throw = false) : ICommand<Guid>;

public sealed record GetThing(Guid Id) : IQuery<string>;

public sealed record Ping : ICommand;

internal sealed class CreateThingValidator : AbstractValidator<CreateThing>
{
    public CreateThingValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithErrorCode("thing.title_required").MaximumLength(10).WithErrorCode("thing.title_too_long");
    }
}

internal sealed class GetThingValidator : AbstractValidator<GetThing>
{
    public GetThingValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithErrorCode("thing.id_required");
    }
}

/// <summary>Autorização de recurso de exemplo: nega títulos "secreto" (como um caso sigiloso fora da lista).</summary>
internal sealed class CreateThingAuthorizer(Trace trace) : IResourceAuthorizer<CreateThing>
{
    public static readonly Error Denied = Error.NotFound("thing.not_found", "Não encontrado.");

    public Task<Result> AuthorizeAsync(CreateThing request, CancellationToken cancellationToken)
    {
        trace.Steps.Add("authorize");
        return Task.FromResult(string.Equals(request.Title, "secreto", StringComparison.Ordinal) ? Result.Failure(Denied) : Result.Success);
    }
}

internal sealed class GetThingAuthorizer(Trace trace) : IResourceAuthorizer<GetThing>
{
    public Task<Result> AuthorizeAsync(GetThing request, CancellationToken cancellationToken)
    {
        trace.Steps.Add("authorize-query");
        return Task.FromResult(request.Id == Guid.AllBitsSet ? Result.Failure(CreateThingAuthorizer.Denied) : Result.Success);
    }
}

internal sealed class CreateThingHandler(Trace trace) : ICommandHandler<CreateThing, Guid>
{
    public static readonly Error Rejected = Error.BusinessRule("thing.rejected", "Rejeitado.");

    public Task<Result<Guid>> Handle(CreateThing command, CancellationToken cancellationToken)
    {
        trace.Steps.Add("handler");
        if (command.Throw)
        {
            throw new InvalidOperationException("falha inesperada");
        }

        return Task.FromResult<Result<Guid>>(command.Fail ? Rejected : Guid.CreateVersion7());
    }
}

internal sealed class GetThingHandler(Trace trace) : IQueryHandler<GetThing, string>
{
    public Task<Result<string>> Handle(GetThing query, CancellationToken cancellationToken)
    {
        trace.Steps.Add("query-handler");
        return Task.FromResult<Result<string>>("thing");
    }
}

internal sealed class PingHandler(Trace trace) : ICommandHandler<Ping, Unit>
{
    public Task<Result<Unit>> Handle(Ping command, CancellationToken cancellationToken)
    {
        trace.Steps.Add("ping");
        return Task.FromResult<Result<Unit>>(Unit.Value);
    }
}

/// <summary>Unidade de trabalho falsa que registra begin/commit/rollback no <see cref="Trace"/>.</summary>
public sealed class FakeUnitOfWork(Trace trace) : IUnitOfWork
{
    public bool HasActiveTransaction { get; set; }

    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        trace.Steps.Add("begin");
        HasActiveTransaction = true;
        return Task.FromResult<IUnitOfWorkTransaction>(new FakeTransaction(this, trace));
    }

    private sealed class FakeTransaction(FakeUnitOfWork owner, Trace trace) : IUnitOfWorkTransaction
    {
        private bool _completed;

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            trace.Steps.Add("commit");
            _completed = true;
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken)
        {
            trace.Steps.Add("rollback");
            _completed = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            if (!_completed)
            {
                trace.Steps.Add("dispose-rollback");
            }

            owner.HasActiveTransaction = false;
            return ValueTask.CompletedTask;
        }
    }
}
