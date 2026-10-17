using LexFlow.Application.Abstractions;

namespace LexFlow.BuildingBlocks.UnitTests.Application;

public sealed class TenantOverrideTests
{
    [Fact]
    public void Override_lasts_until_disposed()
    {
        var tenant = new TenantOverride();
        var organization = Guid.CreateVersion7();

        using (tenant.BeginForNewOrganization(organization))
        {
            tenant.OrganizationId.ShouldBe(organization);
        }

        tenant.OrganizationId.ShouldBeNull();
    }

    [Fact]
    public void Override_cannot_be_nested_nor_empty()
    {
        var tenant = new TenantOverride();

        using var first = tenant.BeginForNewOrganization(Guid.CreateVersion7());

        Should.Throw<InvalidOperationException>(() => tenant.BeginForNewOrganization(Guid.CreateVersion7()));
        Should.Throw<ArgumentException>(() => new TenantOverride().BeginForNewOrganization(Guid.Empty));
    }
}
