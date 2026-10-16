// Handlers fora da convenção, usados por HandlerConventionTests.Rule_detects_public_or_unsealed_handlers.
#pragma warning disable CA1812, CA1852, MA0053
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.SharedKernel.Results;

namespace ArchFixture.Handlers;

public sealed record FixtureQuery : IQuery<int>;

public sealed class PublicHandler : IQueryHandler<FixtureQuery, int>
{
    public Task<Result<int>> Handle(FixtureQuery query, CancellationToken cancellationToken) => Task.FromResult<Result<int>>(1);
}

internal class UnsealedHandler : IQueryHandler<FixtureQuery, int>
{
    public Task<Result<int>> Handle(FixtureQuery query, CancellationToken cancellationToken) => Task.FromResult<Result<int>>(1);
}

internal sealed class ConformingHandler : IQueryHandler<FixtureQuery, int>
{
    public Task<Result<int>> Handle(FixtureQuery query, CancellationToken cancellationToken) => Task.FromResult<Result<int>>(1);
}
