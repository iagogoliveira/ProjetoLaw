# ADR 0011 — Dados de referência versionados e MigrationService

- **Status:** Aceita
- **Data:** 2026-10-16
- **Etapa:** 1

## Contexto

A seção 6.5 pede dados de referência (tribunais, feriados) por seed idempotente e versionado em `platform`, e que as migrations nunca rodem no startup da API. A decisão de negócio de 2026-10-16 restringe o seed de feriados aos nacionais de lei federal.

## Decisão

- **Seed nas migrations** (`HasData`). Cada mudança nos dados gera uma nova migration, o que torna o seed versionado e idempotente por construção. IDs determinísticos (UUID v8 derivado da chave natural, ex.: `court:8.26`) mantêm o seed estável entre migrations.
- **Tribunais:** 107 registros pela tabela J.TR da Resolução CNJ 65/2008:
  - STF, CNJ, STJ, CJF, TST, CSJT, TSE e STM;
  - TRF1 a TRF6, TRT1 a TRT24, 27 TREs e 27 TJs;
  - TJMs de MG, RS e SP;
  - 12 Circunscrições Judiciárias Militares.
- **Feriados como regras** (`FixedDate` ou `EasterRelative`), com vigência por ano. As datas de cada ano são calculadas por `HolidayCalendar` (Páscoa pelo algoritmo de Meeus/Jones/Butcher). Entram os nacionais por lei federal: 1/1, 21/4, 1/5, 7/9, 12/10, 2/11, 15/11, 20/11 (desde 2024), 25/12 e Sexta-feira Santa.
- **`LexFlow.MigrationService`.** Aplica as migrations de cada módulo (Platform primeiro), cria o bucket e encerra. No AppHost, API e Worker esperam ele terminar (`WaitForCompletion`). Em produção, o equivalente é o bundle do EF Core no pipeline (seção 8.2).
- **Ferramenta `dotnet-ef`** fixada no manifesto local (`dotnet-tools.json`, 10.0.13, MIT).

## Consequências

- **Etapa 7:** feriados forenses por tribunal, estaduais, municipais, Carnaval, Corpus Christi e recesso (20/12 a 20/01) entram junto com a calculadora de prazos, inclusive os feriados complementados pelo escritório (RF-03).
- **Testes de migration:** do zero, uma a uma sobre a versão anterior, e revertidas até o início e reaplicadas (`MigrationTests`).
