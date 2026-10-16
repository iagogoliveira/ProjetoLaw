# ADR 0001 — Monólito modular com dois processos (API e Worker)

- **Status:** Aceita (definida em `docs/spec/implementacao.md` §3 e §4)
- **Data:** 2026-10-16
- **Etapa:** 0

## Contexto

O LexFlow tem muitos domínios (CRM, contencioso, publicações, prazos, financeiro, GED, IA, portal etc.) e será escrito majoritariamente por agentes de IA. É preciso:

- isolar os domínios para que mudanças fiquem localizadas;
- operar com custo e complexidade baixos no início;
- poder extrair módulos no futuro, se a escala exigir.

## Decisão

- **Uma solução, dois processos .NET:** `LexFlow.Api` (HTTP) e `LexFlow.Worker` (consumidores de mensagens e jobs), compilados da mesma solução.
- **Cada módulo de negócio** em `src/Modules/<Modulo>/` com os projetos `Domain`, `Application`, `Infrastructure`, `Contracts` e `Endpoints`, com as dependências permitidas na tabela da §4.2.
- **Comunicação entre módulos** só via `Contracts` (interfaces públicas e eventos de integração) ou eventos assíncronos pelo outbox.
- **Um schema SQL por módulo** no banco compartilhado.
- **Building blocks** comuns em `src/BuildingBlocks/`: `SharedKernel`, `Application.Abstractions`, `Infrastructure.Common` e `Web.Common`.
- **As fronteiras são verificadas por testes** em `tests/LexFlow.ArchitectureTests`:
  - os projetos de `src/Modules/**` entram nas regras automaticamente (referência por glob);
  - cada camada é identificada pelo nome do assembly, `LexFlow.{Modulo}.{Camada}`.

## Consequências

- Deploy simples (2 containers) e transações locais dentro do módulo.
- Uma violação de fronteira quebra o CI. A prova está no PR de demonstração da Etapa 0 (ver `docs/progress.md`).
- Nomes de projeto fora da convenção `LexFlow.{Modulo}.{Camada}` escapam das regras de módulo. A convenção é obrigatória.
- As regras de dependência olham o uso de tipos (NetArchTest), não as referências de projeto. Uma referência sem uso não é detectada, mas também não causa acoplamento real.
