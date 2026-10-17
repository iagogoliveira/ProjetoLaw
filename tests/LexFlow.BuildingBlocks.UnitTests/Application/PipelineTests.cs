using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Data;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Sample.Application;
using LexFlow.SharedKernel.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace LexFlow.BuildingBlocks.UnitTests.Application;

public sealed class PipelineTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly AsyncServiceScope _scope;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public PipelineTests()
    {
        var services = new ServiceCollection();
        services.AddFakeLogging();
        services.AddScoped<Trace>();
        services.AddKeyedScoped<IUnitOfWork, FakeUnitOfWork>("Sample");
        services.AddApplication(typeof(PipelineTests).Assembly);
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        _scope = _provider.CreateAsyncScope();
    }

    private Trace Trace => _scope.ServiceProvider.GetRequiredService<Trace>();

    private ICommandHandler<CreateThing, Guid> Handler => _scope.ServiceProvider.GetRequiredService<ICommandHandler<CreateThing, Guid>>();

    private FakeLogCollector Logs => _provider.GetFakeLogCollector();

    [Fact]
    public async Task Valid_command_runs_inside_a_committed_transaction()
    {
        var result = await Handler.Handle(new CreateThing("ok"), _ct);

        result.IsSuccess.ShouldBeTrue();
        Trace.Steps.ShouldBe(["authorize", "begin", "handler", "commit"]);
    }

    [Fact]
    public async Task Invalid_command_is_rejected_before_transaction_and_handler()
    {
        var result = await Handler.Handle(new CreateThing(""), _ct);

        var error = result.Error.ShouldBeOfType<ValidationError>();
        error.Errors.ShouldHaveSingleItem().ShouldBe(new FieldError("title", "thing.title_required", error.Errors[0].Message));
        Trace.Steps.ShouldBeEmpty();
    }

    [Fact]
    public async Task Business_failure_rolls_back_the_transaction()
    {
        var result = await Handler.Handle(new CreateThing("ok", Fail: true), _ct);

        result.Error.ShouldBe(CreateThingHandler.Rejected);
        Trace.Steps.ShouldBe(["authorize", "begin", "handler", "rollback"]);
    }

    [Fact]
    public async Task Exception_disposes_the_transaction_without_commit_and_propagates()
    {
        await Should.ThrowAsync<InvalidOperationException>(() => Handler.Handle(new CreateThing("ok", Throw: true), _ct));

        Trace.Steps.ShouldBe(["authorize", "begin", "handler", "dispose-rollback"]);
        Logs.GetSnapshot().ShouldContain(r => r.Level == LogLevel.Error && r.Exception is InvalidOperationException);
    }

    [Fact]
    public async Task Nested_command_reuses_the_active_transaction()
    {
        var unitOfWork = (FakeUnitOfWork)_scope.ServiceProvider.GetRequiredKeyedService<IUnitOfWork>("Sample");
        unitOfWork.HasActiveTransaction = true;

        await Handler.Handle(new CreateThing("ok"), _ct);

        Trace.Steps.ShouldBe(["authorize", "handler"]);
    }

    [Fact]
    public async Task Logging_records_name_and_error_code_but_not_the_payload()
    {
        await Handler.Handle(new CreateThing("segredo", Fail: true), _ct);

        var record = Logs.GetSnapshot().ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Warning);
        record.Message.ShouldContain("CreateThing");
        record.Message.ShouldContain("thing.rejected");
        record.Message.ShouldNotContain("segredo");
    }

    [Fact]
    public async Task Query_is_validated_and_never_opens_a_transaction()
    {
        var handler = _scope.ServiceProvider.GetRequiredService<IQueryHandler<GetThing, string>>();

        (await handler.Handle(new GetThing(Guid.Empty), _ct)).Error.ShouldBeOfType<ValidationError>();
        (await handler.Handle(new GetThing(Guid.CreateVersion7()), _ct)).Value.ShouldBe("thing");
        Trace.Steps.ShouldBe(["authorize-query", "query-handler"]);
    }

    [Fact]
    public async Task Resource_authorization_runs_after_validation_and_before_the_transaction()
    {
        var denied = await Handler.Handle(new CreateThing("secreto"), _ct);

        denied.Error.ShouldBe(CreateThingAuthorizer.Denied);
        Trace.Steps.ShouldBe(["authorize"]);
    }

    [Fact]
    public async Task Denied_query_never_reaches_the_handler()
    {
        var handler = _scope.ServiceProvider.GetRequiredService<IQueryHandler<GetThing, string>>();

        (await handler.Handle(new GetThing(Guid.AllBitsSet), _ct)).Error.ShouldBe(CreateThingAuthorizer.Denied);
        Trace.Steps.ShouldBe(["authorize-query"]);
    }

    [Fact]
    public async Task Command_without_value_returns_unit()
    {
        var handler = _scope.ServiceProvider.GetRequiredService<ICommandHandler<Ping, Unit>>();

        (await handler.Handle(new Ping(), _ct)).Value.ShouldBe(Unit.Value);
        Trace.Steps.ShouldBe(["begin", "ping", "commit"]);
    }

    [Theory]
    [InlineData(typeof(CreateThing), "Sample")]
    [InlineData(typeof(PipelineTests), null)]
    [InlineData(typeof(string), null)]
    public void Module_name_comes_from_namespace(Type type, string? expected) => ModuleName.Of(type).ShouldBe(expected);

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }
}
