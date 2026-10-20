using LexFlow.Scheduling.Domain.Calendar;

namespace LexFlow.Scheduling.UnitTests;

/// <summary>Um caso de prazo da suíte de aceite da Etapa 7 (critério 1). Datas no formato ISO.</summary>
public sealed record DeadlineCase(string Id, string Description, DeadlineStartKind StartKind, string Trigger, string? ConsultedOn, int Days,
    CountingMode Mode, bool Doubled, string Calendar, string? Office, int InternalLeadDays, string IntimationOn, string StartsOn, string FatalDueOn,
    string InternalDueOn)
{
    public override string ToString() => $"{Id}: {Description}";
}

/// <summary>
/// Casos de prazo redigidos com o fundamento de cada grupo e conferidos contra uma implementação de referência independente.
/// <b>Revisão por advogado pendente</b>: o critério 1 da Etapa 7 só é dado como atendido depois dela (decisão de 2026-10-20).
/// Calendários: nacionais e recesso sempre; "SP", "RJ", "ES", "PE" e "JF" acrescentam os do estado ou da Justiça Federal; o
/// escritório pode acrescentar "MUN" (feriado municipal em 19/03/2026), "OUT" (indisponibilidade em 15/05/2026), "OUT2"
/// (indisponibilidade em 02/03/2026) ou "SUS" (suspensão de 08 a 12/06/2026).
/// </summary>
public static class DeadlineCases
{
    public static IReadOnlyList<DeadlineCase> All { get; } =
    [
        new("P01", "15 dias úteis, publicação numa segunda sem feriados", DeadlineStartKind.Publication, "2026-03-02", null, 15, CountingMode.BusinessDays, false, "", null, 0, "2026-03-02", "2026-03-03", "2026-03-23", "2026-03-23"),
        new("P02", "15 dias úteis, publicação numa sexta: começa na segunda", DeadlineStartKind.Publication, "2026-03-06", null, 15, CountingMode.BusinessDays, false, "", null, 0, "2026-03-06", "2026-03-09", "2026-03-27", "2026-03-27"),
        new("P03", "15 dias úteis atravessando a Sexta-feira Santa e Tiradentes", DeadlineStartKind.Publication, "2026-03-30", null, 15, CountingMode.BusinessDays, false, "", null, 0, "2026-03-30", "2026-03-31", "2026-04-22", "2026-04-22"),
        new("P04", "5 dias úteis com o 1º de maio no meio", DeadlineStartKind.Publication, "2026-04-28", null, 5, CountingMode.BusinessDays, false, "", null, 0, "2026-04-28", "2026-04-29", "2026-05-06", "2026-05-06"),
        new("P05", "1 dia útil: vence no dia útil seguinte", DeadlineStartKind.Publication, "2026-06-03", null, 1, CountingMode.BusinessDays, false, "", null, 0, "2026-06-03", "2026-06-04", "2026-06-04", "2026-06-04"),
        new("P06", "1 dia útil numa véspera de feriado nacional (Independência na segunda)", DeadlineStartKind.Publication, "2026-09-04", null, 1, CountingMode.BusinessDays, false, "", null, 0, "2026-09-04", "2026-09-08", "2026-09-08", "2026-09-08"),
        new("P07", "15 dias úteis com 7/9 e 12/10 (segundas)", DeadlineStartKind.Publication, "2026-09-01", null, 15, CountingMode.BusinessDays, false, "", null, 0, "2026-09-01", "2026-09-02", "2026-09-23", "2026-09-23"),
        new("P08", "15 dias úteis com Finados e Consciência Negra", DeadlineStartKind.Publication, "2026-10-28", null, 15, CountingMode.BusinessDays, false, "", null, 0, "2026-10-28", "2026-10-29", "2026-11-19", "2026-11-19"),
        new("P09", "Carnaval não é feriado nacional: conta na Justiça comum sem lei local", DeadlineStartKind.Publication, "2026-02-13", null, 5, CountingMode.BusinessDays, false, "", null, 0, "2026-02-13", "2026-02-16", "2026-02-20", "2026-02-20"),
        new("P10", "30 dias úteis em 2025 com Sexta-feira Santa e Tiradentes", DeadlineStartKind.Publication, "2025-04-01", null, 30, CountingMode.BusinessDays, false, "", null, 0, "2025-04-01", "2025-04-02", "2025-05-16", "2025-05-16"),
        new("P11", "15 dias úteis em 2025 com Consciência Negra numa quinta", DeadlineStartKind.Publication, "2025-11-10", null, 15, CountingMode.BusinessDays, false, "", null, 0, "2025-11-10", "2025-11-11", "2025-12-02", "2025-12-02"),
        new("P12", "10 dias úteis com 15/11 num domingo (2026)", DeadlineStartKind.Publication, "2026-11-09", null, 10, CountingMode.BusinessDays, false, "", null, 0, "2026-11-09", "2026-11-10", "2026-11-24", "2026-11-24"),
        new("P13", "8 dias úteis (recurso trabalhista) sem feriados", DeadlineStartKind.Publication, "2026-08-03", null, 8, CountingMode.BusinessDays, false, "", null, 0, "2026-08-03", "2026-08-04", "2026-08-13", "2026-08-13"),
        new("P14", "3 dias úteis terminando numa sexta", DeadlineStartKind.Publication, "2026-08-04", null, 3, CountingMode.BusinessDays, false, "", null, 0, "2026-08-04", "2026-08-05", "2026-08-07", "2026-08-07"),
        new("R01", "Publicação em 18/12: o prazo volta a correr em 21/01", DeadlineStartKind.Publication, "2026-12-18", null, 15, CountingMode.BusinessDays, false, "", null, 0, "2026-12-18", "2027-01-21", "2027-02-10", "2027-02-10"),
        new("R02", "Prazo iniciado em dezembro fica suspenso no recesso e retoma em 21/01", DeadlineStartKind.Publication, "2026-12-10", null, 15, CountingMode.BusinessDays, false, "", null, 0, "2026-12-10", "2026-12-11", "2027-02-02", "2027-02-02"),
        new("R03", "Publicação durante o recesso: começa em 21/01", DeadlineStartKind.Publication, "2027-01-08", null, 5, CountingMode.BusinessDays, false, "", null, 0, "2027-01-08", "2027-01-21", "2027-01-27", "2027-01-27"),
        new("R04", "Recesso 2025/2026 com prazo longo (30 dias)", DeadlineStartKind.Publication, "2025-12-01", null, 30, CountingMode.BusinessDays, false, "", null, 0, "2025-12-01", "2025-12-02", "2026-02-11", "2026-02-11"),
        new("R05", "Dias corridos também param no recesso", DeadlineStartKind.Publication, "2026-12-14", null, 10, CountingMode.CalendarDays, false, "", null, 0, "2026-12-14", "2026-12-15", "2027-01-25", "2027-01-25"),
        new("R06", "Publicação em 19/12 (sábado não há; sexta 18/12 em 2026) com 1 dia", DeadlineStartKind.Publication, "2026-12-18", null, 1, CountingMode.BusinessDays, false, "", null, 0, "2026-12-18", "2027-01-21", "2027-01-21", "2027-01-21"),
        new("J01", "DJEN disponibilizado na quinta: publicado na sexta, conta da segunda", DeadlineStartKind.DjenAvailability, "2026-03-05", null, 15, CountingMode.BusinessDays, false, "", null, 0, "2026-03-06", "2026-03-09", "2026-03-27", "2026-03-27"),
        new("J02", "DJEN disponibilizado na sexta: publicado na segunda, conta da terça", DeadlineStartKind.DjenAvailability, "2026-03-06", null, 15, CountingMode.BusinessDays, false, "", null, 0, "2026-03-09", "2026-03-10", "2026-03-30", "2026-03-30"),
        new("J03", "DJEN disponibilizado na véspera da Sexta-feira Santa", DeadlineStartKind.DjenAvailability, "2026-04-02", null, 5, CountingMode.BusinessDays, false, "", null, 0, "2026-04-06", "2026-04-07", "2026-04-13", "2026-04-13"),
        new("J04", "DJEN disponibilizado em 30/04 (1º de maio é sexta)", DeadlineStartKind.DjenAvailability, "2026-04-30", null, 15, CountingMode.BusinessDays, false, "", null, 0, "2026-05-04", "2026-05-05", "2026-05-25", "2026-05-25"),
        new("J05", "DJEN disponibilizado em 17/12: publicado em 18/12, prazo suspenso no recesso", DeadlineStartKind.DjenAvailability, "2026-12-17", null, 15, CountingMode.BusinessDays, false, "", null, 0, "2026-12-18", "2027-01-21", "2027-02-10", "2027-02-10"),
        new("J06", "DJEN disponibilizado em 18/12: publicação presumida só em 21/01", DeadlineStartKind.DjenAvailability, "2026-12-18", null, 15, CountingMode.BusinessDays, false, "", null, 0, "2027-01-21", "2027-01-22", "2027-02-11", "2027-02-11"),
        new("J07", "DJEN com 5 dias úteis e internal 2 dias antes", DeadlineStartKind.DjenAvailability, "2026-06-15", null, 5, CountingMode.BusinessDays, false, "", null, 2, "2026-06-16", "2026-06-17", "2026-06-23", "2026-06-19"),
        new("J08", "DJEN com prazo de 30 dias úteis em dobro (Fazenda)", DeadlineStartKind.DjenAvailability, "2026-03-09", null, 30, CountingMode.BusinessDays, true, "", null, 0, "2026-03-10", "2026-03-11", "2026-06-05", "2026-06-05"),
        new("E01", "Consulta no 3º dia: intimação na consulta", DeadlineStartKind.PortalIntimation, "2026-03-02", "2026-03-05", 15, CountingMode.BusinessDays, false, "", null, 0, "2026-03-05", "2026-03-06", "2026-03-26", "2026-03-26"),
        new("E02", "Sem consulta: intimação tácita no 10º dia (dia útil)", DeadlineStartKind.PortalIntimation, "2026-03-02", null, 15, CountingMode.BusinessDays, false, "", null, 0, "2026-03-12", "2026-03-13", "2026-04-02", "2026-04-02"),
        new("E03", "Sem consulta: 10º dia cai no sábado e passa para a segunda", DeadlineStartKind.PortalIntimation, "2026-03-04", null, 15, CountingMode.BusinessDays, false, "", null, 0, "2026-03-16", "2026-03-17", "2026-04-07", "2026-04-07"),
        new("E04", "Consulta num domingo: intimação na segunda", DeadlineStartKind.PortalIntimation, "2026-03-02", "2026-03-08", 5, CountingMode.BusinessDays, false, "", null, 0, "2026-03-09", "2026-03-10", "2026-03-16", "2026-03-16"),
        new("E05", "Consulta depois dos 10 dias não adia a intimação tácita", DeadlineStartKind.PortalIntimation, "2026-03-02", "2026-03-20", 5, CountingMode.BusinessDays, false, "", null, 0, "2026-03-12", "2026-03-13", "2026-03-19", "2026-03-19"),
        new("E06", "Consulta no próprio dia do envio", DeadlineStartKind.PortalIntimation, "2026-08-03", "2026-08-03", 10, CountingMode.BusinessDays, false, "", null, 0, "2026-08-03", "2026-08-04", "2026-08-17", "2026-08-17"),
        new("E07", "Tácita caindo em Tiradentes passa para o dia útil seguinte", DeadlineStartKind.PortalIntimation, "2026-04-11", null, 5, CountingMode.BusinessDays, false, "", null, 0, "2026-04-22", "2026-04-23", "2026-04-29", "2026-04-29"),
        new("E08", "Envio no recesso: tácita em 31/12 vai a 21/01", DeadlineStartKind.PortalIntimation, "2026-12-21", null, 5, CountingMode.BusinessDays, false, "", null, 0, "2027-01-21", "2027-01-22", "2027-01-28", "2027-01-28"),
        new("F01", "Juntada do AR numa quarta", DeadlineStartKind.EventDate, "2026-05-06", null, 15, CountingMode.BusinessDays, false, "", null, 0, "2026-05-06", "2026-05-07", "2026-05-27", "2026-05-27"),
        new("F02", "Ciência numa sexta: começa na segunda", DeadlineStartKind.EventDate, "2026-05-08", null, 10, CountingMode.BusinessDays, false, "", null, 0, "2026-05-08", "2026-05-11", "2026-05-22", "2026-05-22"),
        new("F03", "Juntada num sábado (sistema): começa no primeiro dia útil", DeadlineStartKind.EventDate, "2026-05-09", null, 5, CountingMode.BusinessDays, false, "", null, 0, "2026-05-09", "2026-05-11", "2026-05-15", "2026-05-15"),
        new("F04", "Juntada na véspera de feriado nacional (Independência)", DeadlineStartKind.EventDate, "2026-09-04", null, 5, CountingMode.BusinessDays, false, "", null, 0, "2026-09-04", "2026-09-08", "2026-09-14", "2026-09-14"),
        new("D01", "15 dias em dobro (30 dias úteis)", DeadlineStartKind.Publication, "2026-03-02", null, 15, CountingMode.BusinessDays, true, "", null, 0, "2026-03-02", "2026-03-03", "2026-04-14", "2026-04-14"),
        new("D02", "5 dias em dobro (10 dias úteis) com Sexta-feira Santa", DeadlineStartKind.Publication, "2026-03-30", null, 5, CountingMode.BusinessDays, true, "", null, 0, "2026-03-30", "2026-03-31", "2026-04-14", "2026-04-14"),
        new("D03", "15 dias em dobro atravessando o recesso", DeadlineStartKind.Publication, "2026-12-01", null, 15, CountingMode.BusinessDays, true, "", null, 0, "2026-12-01", "2026-12-02", "2027-02-12", "2027-02-12"),
        new("D04", "Prazo corrido em dobro (10 → 20 dias)", DeadlineStartKind.Publication, "2026-08-03", null, 10, CountingMode.CalendarDays, true, "", null, 0, "2026-08-03", "2026-08-04", "2026-08-24", "2026-08-24"),
        new("C01", "5 dias corridos terminando no sábado: prorroga para segunda", DeadlineStartKind.Publication, "2026-03-02", null, 5, CountingMode.CalendarDays, false, "", null, 0, "2026-03-02", "2026-03-03", "2026-03-09", "2026-03-09"),
        new("C02", "10 dias corridos sem prorrogação", DeadlineStartKind.Publication, "2026-03-03", null, 10, CountingMode.CalendarDays, false, "", null, 0, "2026-03-03", "2026-03-04", "2026-03-13", "2026-03-13"),
        new("C03", "Publicação na sexta: corridos começam na segunda (CPC, art. 224, § 1º)", DeadlineStartKind.Publication, "2026-03-06", null, 5, CountingMode.CalendarDays, false, "", null, 0, "2026-03-06", "2026-03-09", "2026-03-13", "2026-03-13"),
        new("C04", "Corridos vencendo na Sexta-feira Santa prorrogam até segunda", DeadlineStartKind.Publication, "2026-03-29", null, 5, CountingMode.CalendarDays, false, "", null, 0, "2026-03-29", "2026-03-30", "2026-04-06", "2026-04-06"),
        new("C05", "Corridos contam feriado no meio (Tiradentes)", DeadlineStartKind.Publication, "2026-04-16", null, 10, CountingMode.CalendarDays, false, "", null, 0, "2026-04-16", "2026-04-17", "2026-04-27", "2026-04-27"),
        new("C06", "30 dias corridos", DeadlineStartKind.Publication, "2026-06-01", null, 30, CountingMode.CalendarDays, false, "", null, 0, "2026-06-01", "2026-06-02", "2026-07-01", "2026-07-01"),
        new("S01", "SP: Revolução Constitucionalista (9/7) não conta", DeadlineStartKind.Publication, "2026-07-06", null, 5, CountingMode.BusinessDays, false, "SP", null, 0, "2026-07-06", "2026-07-07", "2026-07-14", "2026-07-14"),
        new("S02", "Mesmo prazo fora de SP conta o 9/7", DeadlineStartKind.Publication, "2026-07-06", null, 5, CountingMode.BusinessDays, false, "", null, 0, "2026-07-06", "2026-07-07", "2026-07-13", "2026-07-13"),
        new("S03", "RJ: São Jorge (23/4) e Tiradentes", DeadlineStartKind.Publication, "2026-04-17", null, 5, CountingMode.BusinessDays, false, "RJ", null, 0, "2026-04-17", "2026-04-20", "2026-04-28", "2026-04-28"),
        new("S04", "RJ: terça de Carnaval é feriado estadual", DeadlineStartKind.Publication, "2026-02-12", null, 5, CountingMode.BusinessDays, false, "RJ", null, 0, "2026-02-12", "2026-02-13", "2026-02-20", "2026-02-20"),
        new("S05", "RJ: Corpus Christi por lei estadual a partir de 2026", DeadlineStartKind.Publication, "2026-06-01", null, 5, CountingMode.BusinessDays, false, "RJ", null, 0, "2026-06-01", "2026-06-02", "2026-06-09", "2026-06-09"),
        new("S06", "ES: Nossa Senhora da Penha (segunda após a Páscoa)", DeadlineStartKind.Publication, "2026-04-08", null, 5, CountingMode.BusinessDays, false, "ES", null, 0, "2026-04-08", "2026-04-09", "2026-04-16", "2026-04-16"),
        new("S07", "PE: Data Magna (6/3) numa sexta", DeadlineStartKind.Publication, "2026-03-03", null, 5, CountingMode.BusinessDays, false, "PE", null, 0, "2026-03-03", "2026-03-04", "2026-03-11", "2026-03-11"),
        new("S08", "Vencimento corrido num feriado estadual prorroga (SP)", DeadlineStartKind.Publication, "2026-07-03", null, 6, CountingMode.CalendarDays, false, "SP", null, 0, "2026-07-03", "2026-07-06", "2026-07-13", "2026-07-13"),
        new("JF1", "JF: Semana Santa de quarta a domingo", DeadlineStartKind.Publication, "2026-03-30", null, 5, CountingMode.BusinessDays, false, "JF", null, 0, "2026-03-30", "2026-03-31", "2026-04-09", "2026-04-09"),
        new("JF2", "JF: segunda e terça de Carnaval", DeadlineStartKind.Publication, "2026-02-12", null, 5, CountingMode.BusinessDays, false, "JF", null, 0, "2026-02-12", "2026-02-13", "2026-02-23", "2026-02-23"),
        new("JF3", "JF: 11 de agosto", DeadlineStartKind.Publication, "2026-08-07", null, 3, CountingMode.BusinessDays, false, "JF", null, 0, "2026-08-07", "2026-08-10", "2026-08-13", "2026-08-13"),
        new("JF4", "JF: 8 de dezembro antes do recesso", DeadlineStartKind.Publication, "2026-12-03", null, 5, CountingMode.BusinessDays, false, "JF", null, 0, "2026-12-03", "2026-12-04", "2026-12-11", "2026-12-11"),
        new("JF5", "JF: 1º de novembro num domingo e Finados", DeadlineStartKind.Publication, "2026-10-29", null, 3, CountingMode.BusinessDays, false, "JF", null, 0, "2026-10-29", "2026-10-30", "2026-11-04", "2026-11-04"),
        new("O01", "Feriado municipal cadastrado pelo escritório", DeadlineStartKind.Publication, "2026-03-16", null, 5, CountingMode.BusinessDays, false, "", "MUN", 0, "2026-03-16", "2026-03-17", "2026-03-24", "2026-03-24"),
        new("O02", "Indisponibilidade no vencimento prorroga (dias úteis)", DeadlineStartKind.Publication, "2026-05-08", null, 5, CountingMode.BusinessDays, false, "", "OUT", 0, "2026-05-08", "2026-05-11", "2026-05-18", "2026-05-18"),
        new("O03", "Indisponibilidade no vencimento prorroga (dias corridos)", DeadlineStartKind.Publication, "2026-05-10", null, 5, CountingMode.CalendarDays, false, "", "OUT", 0, "2026-05-10", "2026-05-11", "2026-05-18", "2026-05-18"),
        new("O04", "Suspensão determinada pelo tribunal por uma semana", DeadlineStartKind.Publication, "2026-06-03", null, 5, CountingMode.BusinessDays, false, "", "SUS", 0, "2026-06-03", "2026-06-04", "2026-06-17", "2026-06-17"),
        new("O05", "Suspensão do tribunal também para os dias corridos", DeadlineStartKind.Publication, "2026-06-05", null, 5, CountingMode.CalendarDays, false, "", "SUS", 0, "2026-06-05", "2026-06-15", "2026-06-19", "2026-06-19"),
        new("O06", "Indisponibilidade no primeiro dia: começa no dia útil seguinte", DeadlineStartKind.Publication, "2026-02-27", null, 3, CountingMode.BusinessDays, false, "", "OUT2", 0, "2026-02-27", "2026-03-03", "2026-03-05", "2026-03-05"),
        new("I01", "Data interna 3 dias úteis antes do vencimento", DeadlineStartKind.Publication, "2026-03-02", null, 15, CountingMode.BusinessDays, false, "", null, 3, "2026-03-02", "2026-03-03", "2026-03-23", "2026-03-18"),
        new("I02", "Data interna não passa do primeiro dia contado", DeadlineStartKind.Publication, "2026-03-02", null, 2, CountingMode.BusinessDays, false, "", null, 5, "2026-03-02", "2026-03-03", "2026-03-04", "2026-03-03"),
        new("I03", "Data interna pula o feriado ao recuar", DeadlineStartKind.Publication, "2026-04-14", null, 5, CountingMode.BusinessDays, false, "", null, 2, "2026-04-14", "2026-04-15", "2026-04-22", "2026-04-17"),
    ];

    public static TheoryData<DeadlineCase> Data => [.. All];
}
