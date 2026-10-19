using LexFlow.Publications.Domain;

namespace LexFlow.Publications.UnitTests;

public sealed class PublicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 10, 0, 0, TimeSpan.FromHours(-3));
    private static readonly DateOnly Day = new(2026, 10, 19);
    private const string Cnj = "2238058-98.2025.8.26.8434";

    [Theory]
    [InlineData(CaptureSourceKind.Oab, "OAB/SP 001234", "1234/SP")]
    [InlineData(CaptureSourceKind.Name, " Maria Silva ", "Maria Silva")]
    [InlineData(CaptureSourceKind.Cnpj, "11.444.777/0001-61", "11444777000161")]
    public void Sources_use_existing_value_objects(CaptureSourceKind kind, string value, string expected)
    {
        var source = CaptureSource.Create(new(kind, " Fonte ", value)).Value;
        source.Value.ShouldBe(expected);
        source.Label.ShouldBe("Fonte");
        source.IsActive.ShouldBeTrue();
        source.Update(new(kind, "Outra", value, false)).IsSuccess.ShouldBeTrue();
        source.IsActive.ShouldBeFalse();
    }

    [Theory]
    [InlineData(CaptureSourceKind.Oab, "XX 123")]
    [InlineData(CaptureSourceKind.Oab, "SP 000")]
    [InlineData(CaptureSourceKind.Cnpj, "11111111111111")]
    [InlineData(CaptureSourceKind.Name, " ")]
    [InlineData((CaptureSourceKind)99, "Maria")]
    public void Invalid_sources_return_a_stable_error(CaptureSourceKind kind, string value) =>
        CaptureSource.Create(new(kind, "Fonte", value)).Error!.Code.ShouldBe("publications.source_invalid");

    [Fact]
    public void Source_update_is_atomic_when_invalid()
    {
        var source = CaptureSource.Create(new(CaptureSourceKind.Name, "Nome", "Maria")).Value;
        source.Update(new(CaptureSourceKind.Cnpj, "Mudou", "invalid")).IsFailure.ShouldBeTrue();
        source.Kind.ShouldBe(CaptureSourceKind.Name);
        source.Label.ShouldBe("Nome");
    }

    [Theory]
    [InlineData("2238058-98.2025.8.26.8434", "22380589820258268434")]
    [InlineData("22380589820258268434", "22380589820258268434")]
    [InlineData("2238058-00.2025.8.26.8434", null)]
    [InlineData("Sem número", null)]
    [InlineData("2238058-98.2025.8.26.8434 e 6616920-53.2022.4.01.1974", null)]
    [InlineData("2238058-98.2025.8.26.8434 repetido 22380589820258268434", "22380589820258268434")]
    public void Only_one_distinct_valid_cnj_can_be_extracted(string text, string? expected) => Publication.ExtractCnj(text).ShouldBe(expected);

    [Fact]
    public void Hash_uses_journal_day_and_normalized_text()
    {
        var source = Guid.CreateVersion7();
        var first = Publication.Receive(new(source, " DJE ", Day, Day, " Intimação\n   Maria "), Now).Value;
        var same = Publication.Receive(new(source, "dje", Day, Day, "INTIMAÇÃO MARIA"), Now.AddHours(1)).Value;
        first.Hash.ShouldBe(same.Hash);
        Publication.Receive(new(source, "Outro", Day, Day, first.Text), Now).Value.Hash.ShouldNotBe(first.Hash);
        Publication.Receive(new(source, first.Journal, Day, Day.AddDays(1), first.Text), Now).Value.Hash.ShouldNotBe(first.Hash);
        first.ReceivedAt.Offset.ShouldBe(TimeSpan.Zero);
        first.Status.ShouldBe(PublicationStatus.Pending);
        first.Link(Guid.Empty).IsFailure.ShouldBeTrue();
        var caseId = Guid.CreateVersion7();
        first.Link(caseId).IsSuccess.ShouldBeTrue();
        first.LegalCaseId.ShouldBe(caseId);
    }

    [Fact]
    public void Publications_validate_required_fields_and_dates()
    {
        Publication.Receive(new(Guid.Empty, "DJE", Day, Day, "Texto"), Now).IsFailure.ShouldBeTrue();
        Publication.Receive(new(Guid.CreateVersion7(), " ", Day, Day, "Texto"), Now).IsFailure.ShouldBeTrue();
        Publication.Receive(new(Guid.CreateVersion7(), "DJE", Day, Day, " "), Now).IsFailure.ShouldBeTrue();
        Publication.Receive(new(Guid.CreateVersion7(), "DJE", Day, Day.AddDays(-1), "Texto"), Now).IsFailure.ShouldBeTrue();
        Publication.Receive(new(Guid.CreateVersion7(), "DJE", Day, Day, Cnj), Now).Value.ExtractedCnj.ShouldBe("22380589820258268434");
    }

    [Fact]
    public void Manual_movements_keep_event_time_and_collection_time_in_utc()
    {
        var caseId = Guid.CreateVersion7();
        var movement = CaseMovement.Create(caseId, new(Now.AddDays(-2), " Sentença "), Now).Value;
        movement.Description.ShouldBe("Sentença");
        movement.LegalCaseId.ShouldBe(caseId);
        movement.Origin.ShouldBe(MovementOrigin.Manual);
        movement.OccurredAt.Offset.ShouldBe(TimeSpan.Zero);
        movement.CollectedAt.ShouldBe(Now.ToUniversalTime());
        CaseMovement.Create(Guid.Empty, new(Now, "A"), Now).IsFailure.ShouldBeTrue();
        CaseMovement.Create(caseId, new(default, "A"), Now).IsFailure.ShouldBeTrue();
        CaseMovement.Create(caseId, new(Now, ""), Now).IsFailure.ShouldBeTrue();
        CaseMovement.Create(caseId, new(Now, "A"), Now, (MovementOrigin)99).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Capture_runs_can_finish_once_with_success_or_failure()
    {
        var source = Guid.CreateVersion7();
        CaptureRun.Start(Guid.Empty, Day, Now).IsFailure.ShouldBeTrue();
        CaptureRun.Start(source, default, Now).IsFailure.ShouldBeTrue();
        var run = CaptureRun.Start(source, Day, Now).Value;
        run.Finish(-1, null, Now).IsFailure.ShouldBeTrue();
        run.Finish(0, null, Now.AddMinutes(-1)).IsFailure.ShouldBeTrue();
        run.Finish(0, " ", Now).IsFailure.ShouldBeTrue();
        run.Finish(3, null, Now.AddMinutes(2)).IsSuccess.ShouldBeTrue();
        run.Status.ShouldBe(CaptureRunStatus.Succeeded);
        run.Count.ShouldBe(3);
        run.Finish(4, null, Now).IsFailure.ShouldBeTrue();
        var failed = CaptureRun.Start(source, Day, Now).Value;
        failed.Finish(0, "provider.unavailable", Now).IsSuccess.ShouldBeTrue();
        failed.Status.ShouldBe(CaptureRunStatus.Failed);
        failed.ErrorCode.ShouldBe("provider.unavailable");
        failed.FinishedAt!.Value.Offset.ShouldBe(TimeSpan.Zero);
    }
}
