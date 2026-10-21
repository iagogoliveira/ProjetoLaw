using OrganizationEntity = LexFlow.Organization.Domain.Organizations.Organization;

namespace LexFlow.Organization.UnitTests;

public sealed class DeadlineReviewSettingsTests
{
    [Fact]
    public void Deadline_review_is_disabled_by_default_and_can_be_toggled()
    {
        var organization = OrganizationEntity.Create(Guid.CreateVersion7(), "Escritório");

        organization.RequireDeadlineReview.ShouldBeFalse();

        organization.SetDeadlineReviewRequired(true);
        organization.RequireDeadlineReview.ShouldBeTrue();

        organization.SetDeadlineReviewRequired(false);
        organization.RequireDeadlineReview.ShouldBeFalse();
    }
}
