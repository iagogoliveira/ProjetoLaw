using LexFlow.Litigation.Application.CaseImports;
using LexFlow.Litigation.Contracts;
using LexFlow.Litigation.Domain;
using LexFlow.Litigation.Domain.CaseImports;
using LexFlow.Litigation.Domain.Cases;

namespace LexFlow.Litigation.UnitTests;

/// <summary>Histórico de situação, lote, linha do tempo e o ciclo da importação (PR 5D).</summary>
public sealed class TimelineAndImportTests
{
    private static readonly Guid User = Guid.CreateVersion7();
    private static readonly Guid Lawyer = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private static LegalCase Case() => LegalCase.Create(
        Guid.CreateVersion7(),
        CaseKind.Extrajudicial,
        "CIV-2026-00001",
        new CaseDetails(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            [new PartyInput(Guid.CreateVersion7(), PartyPole.Active, true, null, null)],
            [new AssignmentInput(AssignmentRole.Lawyer, Lawyer, null)])).Value;

    [Fact]
    public void A_status_change_is_recorded_only_when_the_status_changes()
    {
        var caseId = Guid.CreateVersion7();

        CaseStatusChange.Between(caseId, CaseStatus.Active, CaseStatus.Active, null, User, Now).ShouldBeNull();
        var change = CaseStatusChange.Between(caseId, CaseStatus.Active, CaseStatus.Suspended, "  " + new string('x', 600), User, Now)!;

        (change.From, change.To, change.ChangedBy, change.ChangedAt).ShouldBe((CaseStatus.Active, CaseStatus.Suspended, User, Now));
        change.Note!.Length.ShouldBe(CaseStatusChange.MaxNoteLength);
    }

    [Fact]
    public void Redistribution_replaces_or_adds_the_responsible_and_phase_changes_on_open_cases()
    {
        var replaced = Case();
        var added = Case();
        var newcomer = Guid.CreateVersion7();
        var phase = Guid.CreateVersion7();

        replaced.Redistribute(Lawyer, newcomer, AssignmentRole.Partner).IsSuccess.ShouldBeTrue();
        added.Redistribute(null, newcomer, AssignmentRole.Lawyer).IsSuccess.ShouldBeTrue();
        added.Redistribute(null, newcomer, AssignmentRole.Lawyer).IsSuccess.ShouldBeTrue();
        replaced.ChangePhase(phase).IsSuccess.ShouldBeTrue();

        replaced.Assignments.Select(a => (a.Role, a.UserId)).ShouldBe([(AssignmentRole.Partner, (Guid?)newcomer)]);
        added.Assignments.Count.ShouldBe(2);
        replaced.PhaseId.ShouldBe(phase);
        added.Redistribute(Guid.CreateVersion7(), newcomer, AssignmentRole.Lawyer).Error.ShouldBe(LitigationErrors.ResponsibleNotAssigned);
        added.Redistribute(null, newcomer, AssignmentRole.Team).Error.ShouldBe(LitigationErrors.AssignmentInvalid);

        added.Close(new ClosingInput(Guid.CreateVersion7(), CaseOutcome.Settlement, null, new DateOnly(2026, 10, 1), null));
        added.Redistribute(null, Guid.CreateVersion7(), AssignmentRole.Lawyer).Error.ShouldBe(LitigationErrors.CaseClosed);
        added.ChangePhase(null).Error.ShouldBe(LitigationErrors.CaseClosed);
    }

    [Fact]
    public void The_timeline_position_orders_newest_first_and_breaks_ties_by_id()
    {
        var position = new TimelinePosition(Now, "b");
        TimelineEntry Entry(DateTimeOffset at, string id) => new(id, at, "litigation.x", new Dictionary<string, string?>(StringComparer.Ordinal), null);

        position.Precedes(Entry(Now.AddMinutes(-1), "z")).ShouldBeTrue();
        position.Precedes(Entry(Now, "a")).ShouldBeTrue();
        position.Precedes(Entry(Now, "b")).ShouldBeFalse();
        position.Precedes(Entry(Now, "c")).ShouldBeFalse();
        position.Precedes(Entry(Now.AddMinutes(1), "a")).ShouldBeFalse();
    }

    [Fact]
    public void An_import_is_confirmed_once_and_only_with_valid_rows()
    {
        var import = CaseImport.Start(Guid.CreateVersion7(), @"C:\pasta\carteira.csv", Guid.CreateVersion7());
        import.FileName.ShouldBe("carteira.csv");
        import.Confirm(Guid.CreateVersion7()).Error.ShouldBe(LitigationErrors.ImportNotReady);

        var empty = CaseImport.Start(Guid.CreateVersion7(), " ", Guid.CreateVersion7());
        empty.FileName.ShouldBe("planilha");
        empty.Validated(3, 3, "[]");
        empty.Confirm(Guid.CreateVersion7()).Error.ShouldBe(LitigationErrors.ImportNothingValid);

        import.Validated(10, 3, "[]");
        import.ValidRows.ShouldBe(7);
        import.Confirm(Guid.CreateVersion7()).IsSuccess.ShouldBeTrue();
        import.Confirm(Guid.CreateVersion7()).Error.ShouldBe(LitigationErrors.ImportNotReady);
        import.Progress(6, 4);
        import.Progress(12, 3);
        import.Revalidated(10, 3);
        import.Complete(Now);

        (import.Status, import.ImportedRows, import.LastImportedRow, import.CompletedAt).ShouldBe((CaseImportStatus.Completed, 7, 12, (DateTimeOffset?)Now));
        import.Fail("litigation.import_file_invalid", Now);
        import.Status.ShouldBe(CaseImportStatus.Failed);
    }

    [Fact]
    public void An_import_error_keeps_the_cell_and_the_stable_code()
    {
        var error = new CaseImportError(Guid.CreateVersion7(), 5, ImportColumns.Cnj, new string('9', 300), LitigationErrors.CnjInvalid);

        (error.Row, error.Column, error.Code, error.Message).ShouldBe((5, "cnj", "litigation.cnj_invalid", LitigationErrors.CnjInvalid.Message));
        error.Value!.Length.ShouldBe(CaseImportError.MaxValueLength);
    }

    [Theory]
    [InlineData("  Área   Cível ", "area civel")]
    [InlineData("TIPO_AÇÃO", "tipo_acao")]
    [InlineData(null, "")]
    public void Names_are_compared_without_accents_case_or_extra_spaces(string? text, string key) =>
        CaseRowReader.Key(text).ShouldBe(key);
}
