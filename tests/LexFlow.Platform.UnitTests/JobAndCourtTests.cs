using LexFlow.Platform.Domain.Courts;
using LexFlow.Platform.Domain.Jobs;
using LexFlow.SharedKernel.ValueObjects;

namespace LexFlow.Platform.UnitTests;

public sealed class JobAndCourtTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Job_goes_from_queued_to_running_to_succeeded()
    {
        var job = Job.Enqueue(Guid.CreateVersion7(), "documents.import");

        job.Status.ShouldBe(JobStatus.Queued);
        job.Start(Now).IsSuccess.ShouldBeTrue();
        job.Succeed(Now.AddMinutes(1), "/api/v1/documents/1").IsSuccess.ShouldBeTrue();

        job.Status.ShouldBe(JobStatus.Succeeded);
        job.StartedAt.ShouldBe(Now);
        job.CompletedAt.ShouldBe(Now.AddMinutes(1));
        job.ResultLocation.ShouldBe("/api/v1/documents/1");
    }

    [Fact]
    public void Job_can_fail_while_queued_or_running_with_error_code()
    {
        var job = Job.Enqueue(Guid.CreateVersion7(), "documents.import");

        job.Fail(Now, "documents.virus_found").IsSuccess.ShouldBeTrue();

        job.Status.ShouldBe(JobStatus.Failed);
        job.ErrorCode.ShouldBe("documents.virus_found");
    }

    [Fact]
    public void Invalid_transitions_are_rejected()
    {
        var job = Job.Enqueue(Guid.CreateVersion7(), "documents.import");

        job.Succeed(Now, null).Error.ShouldBe(JobErrors.InvalidTransition);
        job.Start(Now);
        job.Start(Now).Error.ShouldBe(JobErrors.InvalidTransition);
        job.Succeed(Now, null);
        job.Fail(Now, "x.y").Error.ShouldBe(JobErrors.InvalidTransition);
    }

    [Fact]
    public void Court_matches_segment_and_code_of_a_cnj_number()
    {
        var tjsp = new Court(Guid.CreateVersion7(), 8, "26", "TJSP", "Tribunal de Justiça (SP)", "SP");

        tjsp.Matches(CnjNumber.Create("2238058-98.2025.8.26.8434").Value).ShouldBeTrue();
        tjsp.Matches(CnjNumber.Create("6882179-53.2025.8.19.4857").Value).ShouldBeFalse();
    }
}
