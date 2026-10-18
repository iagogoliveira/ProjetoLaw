using System.Text.Json.Nodes;
using LexFlow.Platform.Application.CustomFields;
using LexFlow.Platform.Contracts.CustomFields;
using LexFlow.Platform.Domain.CustomFields;

namespace LexFlow.Platform.UnitTests;

public sealed class CustomFieldTests
{
    private static readonly Guid Table = Guid.CreateVersion7();
    private static readonly Guid Option = Guid.CreateVersion7();
    private static readonly Guid OtherOption = Guid.CreateVersion7();
    private static readonly Guid Member = Guid.CreateVersion7();

    private static CustomFieldDefinition Field(string key, CustomFieldType type, bool required = false, CustomFieldSettings? settings = null, int position = 0) =>
        CustomFieldDefinition.Create(Guid.CreateVersion7(), CustomFieldEntities.Cases, key, type, key, null, required, false, position, settings ?? new CustomFieldSettings()).Value;

    private static CustomFieldValidation Validate(IReadOnlyCollection<CustomFieldDefinition> fields, string json) =>
        CustomFieldValueRules.Validate(fields, JsonNode.Parse(json)!.AsObject(), (table, item) => table == Table && item == Option, user => user == Member);

    private static string? CodeOf(CustomFieldValidation result, string key) => result.Errors.SingleOrDefault(e => e.Key == key)?.Code;

    [Fact]
    public void Definition_keeps_only_the_settings_of_its_type()
    {
        var text = Field("observacao", CustomFieldType.Text, settings: new CustomFieldSettings(Minimum: 1, Decimals: 2));
        var number = Field("valor", CustomFieldType.Number, settings: new CustomFieldSettings(MaxLength: 10, Minimum: 0, Maximum: 10, Decimals: 2));
        var currency = Field("causa", CustomFieldType.Currency, settings: new CustomFieldSettings(Decimals: 5));

        text.Settings.ShouldBe(new CustomFieldSettings(MaxLength: CustomFieldDefinition.DefaultTextLength));
        number.Settings.ShouldBe(new CustomFieldSettings(Minimum: 0, Maximum: 10, Decimals: 2));
        currency.Decimals.ShouldBe(2);
        text.IsActive.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Valor", false)]
    [InlineData("1valor", false)]
    [InlineData("valor-causa", false)]
    [InlineData("valor_da_causa_2", true)]
    public void Keys_are_snake_case(string key, bool valid) => CustomFieldDefinition.IsValidKey(key).ShouldBe(valid);

    [Fact]
    public void Inconsistent_settings_are_refused_and_the_list_table_is_fixed()
    {
        CustomFieldDefinition.Create(Guid.CreateVersion7(), "cases", "Inválida", CustomFieldType.Text, "x", null, false, false, 0, new()).Error.ShouldBe(CustomFieldErrors.InvalidKey);
        CustomFieldDefinition.Create(Guid.CreateVersion7(), "cases", "faixa", CustomFieldType.Number, "x", null, false, false, 0, new(Minimum: 5, Maximum: 1)).Error.ShouldBe(CustomFieldErrors.InvalidRange);
        CustomFieldDefinition.Create(Guid.CreateVersion7(), "cases", "texto", CustomFieldType.Text, "x", null, false, false, 0, new(MaxLength: 5000)).Error.ShouldBe(CustomFieldErrors.InvalidMaxLength);
        CustomFieldDefinition.Create(Guid.CreateVersion7(), "cases", "numero", CustomFieldType.Number, "x", null, false, false, 0, new(Decimals: 7)).Error.ShouldBe(CustomFieldErrors.InvalidDecimals);
        CustomFieldDefinition.Create(Guid.CreateVersion7(), "cases", "lista", CustomFieldType.List, "x", null, false, false, 0, new()).Error.ShouldBe(CustomFieldErrors.LookupTableRequired);

        var list = Field("fase", CustomFieldType.List, settings: new CustomFieldSettings(LookupTableId: Table));
        list.Update("Fase atual", " Ajuda ", true, true, 3, new CustomFieldSettings(LookupTableId: Table, AllowMultiple: true)).IsSuccess.ShouldBeTrue();
        list.Update("Fase", null, true, true, 3, new CustomFieldSettings(LookupTableId: Guid.CreateVersion7())).Error.ShouldBe(CustomFieldErrors.LookupTableLocked);

        list.Label.ShouldBe("Fase atual");
        list.HelpText.ShouldBe("Ajuda");
        list.AllowMultiple.ShouldBeTrue();
        list.Position.ShouldBe(3);
    }

    [Fact]
    public void Valid_values_are_normalized_for_every_type()
    {
        CustomFieldDefinition[] fields =
        [
            Field("nome", CustomFieldType.Text, settings: new(MaxLength: 10)),
            Field("quantidade", CustomFieldType.Number, settings: new(Minimum: 0, Maximum: 100, Decimals: 1)),
            Field("valor", CustomFieldType.Currency),
            Field("distribuicao", CustomFieldType.Date),
            Field("fase", CustomFieldType.List, settings: new(LookupTableId: Table)),
            Field("pedidos", CustomFieldType.List, settings: new(LookupTableId: Table, AllowMultiple: true)),
            Field("responsavel", CustomFieldType.User),
        ];

        var result = Validate(fields, $$"""
            {"nome": "  Ana  ", "quantidade": 12.5, "valor": 1500.10, "distribuicao": "2026-10-17",
             "fase": "{{Option}}", "pedidos": ["{{Option}}", "{{Option}}"], "responsavel": "{{Member}}"}
            """);

        result.IsValid.ShouldBeTrue(string.Join(", ", result.Errors));
        result.Values["nome"]!.GetValue<string>().ShouldBe("Ana");
        result.Values["quantidade"]!.GetValue<decimal>().ShouldBe(12.5m);
        result.Values["distribuicao"]!.GetValue<string>().ShouldBe("2026-10-17");
        result.Values["fase"]!.GetValue<Guid>().ShouldBe(Option);
        result.Values["pedidos"]!.AsArray().Count.ShouldBe(1);
        result.Values["responsavel"]!.GetValue<Guid>().ShouldBe(Member);
    }

    [Fact]
    public void Each_rule_reports_its_own_code()
    {
        CustomFieldDefinition[] fields =
        [
            Field("obrigatorio", CustomFieldType.Text, required: true),
            Field("curto", CustomFieldType.Text, settings: new(MaxLength: 3)),
            Field("minimo", CustomFieldType.Number, settings: new(Minimum: 10)),
            Field("maximo", CustomFieldType.Currency, settings: new(Maximum: 10)),
            Field("inteiro", CustomFieldType.Number),
            Field("data", CustomFieldType.Date),
            Field("tipo", CustomFieldType.Number),
            Field("fase", CustomFieldType.List, settings: new(LookupTableId: Table)),
            Field("varios", CustomFieldType.List, settings: new(LookupTableId: Table)),
            Field("responsavel", CustomFieldType.User),
        ];

        var result = Validate(fields, $$"""
            {"obrigatorio": "  ", "curto": "abcd", "minimo": 9, "maximo": 10.01, "inteiro": 1.5, "data": "17/10/2026",
             "tipo": "doze", "fase": "{{OtherOption}}", "varios": ["{{Option}}"], "responsavel": "{{Guid.CreateVersion7()}}", "sobrando": 1}
            """);

        CodeOf(result, "obrigatorio").ShouldBe(CustomFieldValueCodes.Required);
        CodeOf(result, "curto").ShouldBe(CustomFieldValueCodes.TooLong);
        CodeOf(result, "minimo").ShouldBe(CustomFieldValueCodes.BelowMinimum);
        CodeOf(result, "maximo").ShouldBe(CustomFieldValueCodes.AboveMaximum);
        CodeOf(result, "inteiro").ShouldBe(CustomFieldValueCodes.TooManyDecimals);
        CodeOf(result, "data").ShouldBe(CustomFieldValueCodes.InvalidDate);
        CodeOf(result, "tipo").ShouldBe(CustomFieldValueCodes.InvalidValue);
        CodeOf(result, "fase").ShouldBe(CustomFieldValueCodes.UnknownOption);
        CodeOf(result, "varios").ShouldBe(CustomFieldValueCodes.InvalidValue);
        CodeOf(result, "responsavel").ShouldBe(CustomFieldValueCodes.UnknownUser);
        CodeOf(result, "sobrando").ShouldBe(CustomFieldValueCodes.UnknownField);
        result.Errors.ShouldAllBe(e => e.Message.Length > 0);
        result.ToValidationError().Errors.Select(e => e.Field).ShouldContain("customFields.obrigatorio");
    }

    [Fact]
    public void Inactive_fields_keep_their_value_and_stop_being_required()
    {
        var field = Field("antigo", CustomFieldType.Text, required: true);
        field.Activate(false);

        var empty = Validate([field], "{}");
        var kept = Validate([field], """{"antigo": 42}""");

        empty.IsValid.ShouldBeTrue();
        kept.IsValid.ShouldBeTrue();
        kept.Values["antigo"]!.GetValue<int>().ShouldBe(42);
    }

    [Fact]
    public void System_lookup_tables_stay_active_and_items_are_updated_in_place()
    {
        var system = LookupTable.Create(Guid.CreateVersion7(), " Fases ", SystemLookupTables.CasePhases);
        var own = LookupTable.Create(Guid.CreateVersion7(), "Origem do cliente");
        var item = LookupItem.Create(Guid.CreateVersion7(), own.Id, " Indicação ", 1);

        system.Activate(false).Error.ShouldBe(CustomFieldErrors.SystemTableLocked);
        own.Activate(false).IsSuccess.ShouldBeTrue();
        item.Update("Indicação de cliente", 2, false);

        system.Name.ShouldBe("Fases");
        own.IsActive.ShouldBeFalse();
        item.Name.ShouldBe("Indicação de cliente");
        item.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Default_lookup_tables_cover_the_system_keys_without_duplicates()
    {
        DefaultLookupTables.All.Select(t => t.SystemKey).ShouldBe(
        [
            SystemLookupTables.ActionTypes, SystemLookupTables.CasePhases, SystemLookupTables.Claims, SystemLookupTables.ClosingReasons, SystemLookupTables.ExpenseTypes, SystemLookupTables.TaskTypes,
            SystemLookupTables.OpportunityOrigins, SystemLookupTables.LossReasons,
        ]);
        DefaultLookupTables.All.ShouldAllBe(t => t.Items.Count > 0 && t.Items.Distinct().Count() == t.Items.Count);
        CustomFieldEntities.Exists("cases").ShouldBeTrue();
        CustomFieldEntities.Exists("tasks").ShouldBeFalse();
    }
}
