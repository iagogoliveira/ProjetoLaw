using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Data;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Sample.Application;
using LexFlow.SharedKernel.Results;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.BuildingBlocks.UnitTests.Application;

public sealed class LocalTransactionPipelineTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly AsyncServiceScope _scope;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public LocalTransactionPipelineTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<Trace>();
        services.AddKeyedScoped<IUnitOfWork, FakeUnitOfWork>("Sample");
        services.AddScoped<ILocalTransactionCoordinator, FakeLocalTransactionCoordinator>();
        services.AddApplication(typeof(LocalTransactionPipelineTests).Assembly);
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        _scope = _provider.CreateAsyncScope();
    }

    private ICommandHandler<CreateLocalThing, Guid> Handler => _scope.ServiceProvider.GetRequiredService<ICommandHandler<CreateLocalThing, Guid>>();

    private List<string> Steps => _scope.ServiceProvider.GetRequiredService<Trace>().Steps;

    [Fact]
    public async Task Opted_in_command_authorizes_then_commits_all_modules_once()
    {
        (await Handler.Handle(new CreateLocalThing("ok"), _ct)).IsSuccess.ShouldBeTrue();

        Steps.ShouldBe(["authorize-local", "local:Sample,Other", "begin", "authorize", "handler", "commit"]);
    }

    [Fact]
    public async Task Invalid_command_never_starts_local_transaction()
    {
        (await Handler.Handle(new CreateLocalThing(""), _ct)).Error.ShouldBeOfType<ValidationError>();

        Steps.ShouldBeEmpty();
    }

    [Fact]
    public async Task Denied_command_never_starts_local_transaction()
    {
        (await Handler.Handle(new CreateLocalThing("denied"), _ct)).Error.ShouldBe(CreateThingAuthorizer.Denied);

        Steps.ShouldBe(["authorize-local"]);
    }

    [Fact]
    public async Task Nested_failure_rolls_back_shared_transaction_once()
    {
        (await Handler.Handle(new CreateLocalThing("ok", Fail: true), _ct)).Error.ShouldBe(CreateThingHandler.Rejected);

        Steps.ShouldBe(["authorize-local", "local:Sample,Other", "begin", "authorize", "handler", "rollback"]);
    }

    [Fact]
    public async Task Nested_exception_disposes_without_commit()
    {
        await Should.ThrowAsync<InvalidOperationException>(() => Handler.Handle(new CreateLocalThing("ok", Throw: true), _ct));

        Steps.ShouldBe(["authorize-local", "local:Sample,Other", "begin", "authorize", "handler", "dispose-rollback"]);
    }

    [Fact]
    public async Task Declaring_another_module_as_owner_is_rejected()
    {
        await Should.ThrowAsync<InvalidOperationException>(() => Handler.Handle(new CreateLocalThing("ok", WrongOwner: true), _ct));

        Steps.ShouldBe(["authorize-local"]);
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }
}
