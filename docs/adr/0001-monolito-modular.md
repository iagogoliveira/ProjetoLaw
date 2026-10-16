# ADR 0001 — Monólito modular

- **Status:** Aceita
- **Data:** 2026-10-16
- **Etapa:** 0

## Contexto

O LexFlow tem muitos domínios (CRM, contencioso, publicações, prazos, financeiro, IA, portal) e precisa ir para produção com uma equipe pequena e custo baixo de infraestrutura, mas sem acoplar os módulos a ponto de impedir uma extração futura. A arquitetura está definida em `docs/spec/implementacao.md`, seções 3 e 4.

## Decisão

- Monorepo com **monólito modular**: dois processos .NET (API e Worker) compilados da mesma solução.
- Cada módulo de negócio tem os projetos `Domain`, `Application`, `Infrastructure`, `Contracts` e `Endpoints`, além de um schema próprio no PostgreSQL.
- Os módulos só se comunicam por `Contracts` (interfaces públicas e eventos de integração) ou por eventos via outbox. Nenhum módulo referencia `Infrastructure` nem as entidades de outro.
- As regras de dependência da seção 4.2 são verificadas por `tests/LexFlow.ArchitectureTests`, e o CI falha quando alguma é violada.

## Consequências

- Deploy e operação simples (2 processos), transações locais dentro de cada módulo e consistência eventual entre módulos.
- As fronteiras dependem dos testes de arquitetura: `LayerRules` descobre módulos pelo namespace `LexFlow.{Modulo}.{Camada}`, e os auto-testes (`LayerRulesSelfTests`) garantem que as regras realmente detectam violações.
- Extrair um módulo para um serviço próprio continua possível, porque a comunicação já é feita por contratos e eventos.
