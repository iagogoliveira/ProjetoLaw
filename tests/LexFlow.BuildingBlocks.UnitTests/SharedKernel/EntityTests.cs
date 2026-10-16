using LexFlow.SharedKernel.Domain;

namespace LexFlow.BuildingBlocks.UnitTests.SharedKernel;

public sealed class EntityTests
{
    private sealed record SomethingHappened(Guid Id) : IDomainEvent;

    private sealed class Sample(Guid id) : AggregateRoot<Guid>(id)
    {
        public void DoSomething() => Raise(new SomethingHappened(Id));
    }

    private sealed class Other(Guid id) : AggregateRoot<Guid>(id);

    [Fact]
    public void Entities_with_same_type_and_id_are_equal()
    {
        var id = Guid.CreateVersion7();

        (new Sample(id) == new Sample(id)).ShouldBeTrue();
        new Sample(id).GetHashCode().ShouldBe(new Sample(id).GetHashCode());
    }

    [Fact]
    public void Entities_with_different_type_or_id_are_not_equal()
    {
        var id = Guid.CreateVersion7();

        new Sample(id).Equals(new Other(id)).ShouldBeFalse();
        (new Sample(id) != new Sample(Guid.CreateVersion7())).ShouldBeTrue();
    }

    [Fact]
    public void Aggregate_collects_and_clears_domain_events()
    {
        var aggregate = new Sample(Guid.CreateVersion7());

        aggregate.DoSomething();
        aggregate.DoSomething();

        aggregate.DomainEvents.Count.ShouldBe(2);
        aggregate.DomainEvents.ShouldAllBe(e => e is SomethingHappened);

        aggregate.ClearDomainEvents();
        aggregate.DomainEvents.ShouldBeEmpty();
    }
}
