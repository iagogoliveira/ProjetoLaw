using FluentValidation;
using LexFlow.Application.Abstractions.Data;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Application.Abstractions.Security;
using LexFlow.SharedKernel.Results;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Sample.Application;

public sealed record CreateLocalThing(string Title, bool Fail = false, bool Throw = false, bool WrongOwner = false)
    : ICommand<Guid>, ILocalTransactionCommand
{
    private static readonly string[] Modules = ["Sample", "Other"];
    private static readonly string[] WrongModules = ["Other", "Sample"];

    public IReadOnlyList<string> TransactionModules => WrongOwner ? WrongModules : Modules;
}

internal sealed class CreateLocalThingValidator : AbstractValidator<CreateLocalThing>
{
    public CreateLocalThingValidator() => RuleFor(x => x.Title).NotEmpty().WithErrorCode("thing.title_required");
}

internal sealed class CreateLocalThingAuthorizer(Trace trace) : IResourceAuthorizer<CreateLocalThing>
{
    public Task<Result> AuthorizeAsync(CreateLocalThing request, CancellationToken cancellationToken)
    {
        trace.Steps.Add("authorize-local");
        return Task.FromResult(request.Title == "denied" ? Result.Failure(CreateThingAuthorizer.Denied) : Result.Success);
    }
}

internal sealed class CreateLocalThingHandler(ICommandHandler<CreateThing, Guid> nested) : ICommandHandler<CreateLocalThing, Guid>
{
    public Task<Result<Guid>> Handle(CreateLocalThing command, CancellationToken cancellationToken) =>
        nested.Handle(new CreateThing(command.Title, command.Fail, command.Throw), cancellationToken);
}

public sealed class FakeLocalTransactionCoordinator(Trace trace, IServiceProvider services) : ILocalTransactionCoordinator
{
    public Task<IUnitOfWorkTransaction> BeginAsync(IReadOnlyList<string> modules, CancellationToken cancellationToken)
    {
        trace.Steps.Add($"local:{string.Join(',', modules)}");
        return services.GetRequiredKeyedService<IUnitOfWork>("Sample").BeginTransactionAsync(cancellationToken);
    }
}
