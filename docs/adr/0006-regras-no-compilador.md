# ADR 0006 — Regras do CLAUDE.md verificadas na compilação e no IL

- **Status:** Aceita
- **Data:** 2026-10-16
- **Etapa:** 1

## Contexto

O `CLAUDE.md` proíbe `DateTime.Now` (usar `IClock`), SQL concatenado e `IgnoreQueryFilters` fora do módulo Platform. Como o código é escrito por agentes, uma regra que só existe em texto acaba sendo violada sem ninguém perceber.

## Decisão

Duas camadas independentes:

1. **Compilação:** `Microsoft.CodeAnalysis.BannedApiAnalyzers` 5.6.0 (MIT) em todos os projetos, via `Directory.Build.props`. Com `TreatWarningsAsErrors`, o RS0030 quebra o build.
   - `BannedSymbols.txt`: `DateTime.Now/UtcNow/Today`, `DateTimeOffset.Now/UtcNow`, `FromSqlRaw`, `ExecuteSqlRaw`, `ExecuteSqlRawAsync`. A alternativa permitida é `FromSql`/`ExecuteSql` com interpolação, que gera parâmetros.
   - `BannedSymbols.TenantFilter.txt`: `IgnoreQueryFilters`, incluído em todos os projetos **exceto** `LexFlow.Platform.*`.
2. **IL:** `BannedApiTests.Production_code_does_not_call_banned_apis` lê com Mono.Cecil (MIT) o IL de todos os assemblies `LexFlow.*` e falha se encontrar chamadas proibidas. O teste vale mesmo se algum projeto desligar o analisador. `Scanner_detects_planted_violations` prova que a varredura funciona.

A implementação de `IClock` usa `TimeProvider.System`, que não está na lista.

## Consequências

- Uma violação aparece no build local e no CI, com a mensagem indicando a alternativa.
- Testes também não podem usar `DateTime.Now`: usam `FakeClock`, como pede a seção 13.3. Um fixture que precise da API proibida suprime o RS0030 explicitamente e de forma visível.
- Para proibir uma nova API, basta adicionar uma linha ao `BannedSymbols.txt` e ao `BannedApiScanner`.
