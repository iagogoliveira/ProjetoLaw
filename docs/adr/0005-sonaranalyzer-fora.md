# ADR 0005 — SonarAnalyzer.CSharp fora até revisão da licença

- **Status:** Aceita (revisão humana em 2026-10-16)
- **Data:** 2026-10-16
- **Etapa:** 0

## Contexto

A tarefa 2 da Etapa 0 pede os analisadores `Meziantou.Analyzer` e `SonarAnalyzer.CSharp`. O `CLAUDE.md` exige conferir a licença antes de adicionar qualquer pacote.

- `Meziantou.Analyzer` 3.0.303: **MIT**.
- `SonarAnalyzer.CSharp` 10.35.0.4138: **SONAR Source-Available License v1.0** (arquivo `licenses/LICENSE.txt` do pacote, atualizado em 13/11/2024). É uma licença *source-available*, não OSI, com uma cláusula que proíbe uso "Competing" com o SonarQube.

A especificação (seção 2) evita de propósito dependências com licença comercial ou restritiva recente.

## Decisão

Não adicionar o `SonarAnalyzer.CSharp` por enquanto. A cobertura de análise vem de:

- analisadores do SDK com `AnalysisLevel=latest-recommended` e `EnforceCodeStyleInBuild=true`;
- `Meziantou.Analyzer`;
- `TreatWarningsAsErrors=true`;
- CodeQL no CI.

## Consequências

- Se a revisão concluir que a SSALv1 é aceitável (o LexFlow não concorre com o SonarQube e o analisador não é redistribuído), basta incluir o pacote em `Directory.Packages.props` e `Directory.Build.props` e registrar a decisão aqui.
- Alternativa: rodar o SonarQube Cloud no CI, sem o pacote no build.
