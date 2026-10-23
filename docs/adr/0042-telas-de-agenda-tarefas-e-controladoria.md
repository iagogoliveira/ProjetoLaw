# ADR 0042 — Telas de agenda, tarefas e controladoria

- **Status:** Aceita (PR #41 mergeado em 2026-10-22)
- **Data:** 2026-10-22
- **Etapa:** 7F (RF-05, RF-24 e RF-26)

## Contexto e decisão

O plano 7A–7G aprovado prevê agenda em dia/semana/mês, tarefas em lista e kanban, redistribuição, controladoria e preferências. Reutilizar os contratos e regras dos PRs #38 e #39. A interface não amplia o acesso definido no servidor; datas da agenda seguem America/Sao_Paulo e janelas semiabertas.

Usar FullCalendar Standard **7.1.3**, licença **MIT**, com o conector oficial **@fullcalendar/angular 7.1.3** (MIT, Angular 16–22), **fullcalendar 7.1.3** (MIT) e **temporal-polyfill 1.0.1** (MIT). Conferidos antes da instalação pelo registro npm (`version`, `license`, `peerDependencies`, `dependencies`) e pela [documentação oficial Angular](https://fullcalendar.io/docs/angular), [migração v7](https://fullcalendar.io/docs/upgrading-from-v6-js) e [licença Standard](https://fullcalendar.io/). Dependências transitivas diretas incluem @fullcalendar/core, @full-ui/headless-calendar e preact, também MIT. As transitivas temporal-spec 1.0.0 (Apache-2.0) e temporal-utils 1.0.1 (MIT) também foram conferidas no npm. Não usar módulos Premium. O polyfill é usado internamente pela biblioteca, sem instalação global.

O kanban usa o Angular CDK já instalado, com alternativa por teclado para mudar estado. Transições só se confirmam após sucesso do servidor; checklist, subtarefas e reabertura mantêm as regras do domínio. Redistribuição mostra resultados individuais. Paginação nunca deve apresentar uma página como conjunto completo.

As janelas do calendário são derivadas da data e da visualização na URL, sem usar os dias de preenchimento da grade mensal como intervalo da consulta. A semana começa na segunda-feira. O CSS Standard é servido como asset local, carregado na agenda; cores usam os tokens Material, sem CDN. Preferências são lidas antes de qualquer gravação e respostas antigas não substituem o contexto atual. Falhas de autorização limpam os dados editáveis do recurso indisponível.

Adicionar opções mínimas de membros/equipes em rotas de Scheduling com leitura do respectivo recurso, para tarefas administrativas não exigirem cases.read só para escolher responsável. Comunicação por contratos de Organization/Identity e tenant atual, sem acesso a outros DbContexts. Vínculos de processo continuam exigindo a leitura correspondente.

Preferências pessoais e políticas do escritório usam as APIs existentes e mostram o piso obrigatório. Resumo diário permanece opt-in às 07:30 de São Paulo. Sincronização externa fica no 7G; anexos dependem do GED da Etapa 9. Revisão jurídica do 7A e fornecedor real da Etapa 6 continuam pendentes.

## Verificação

Testes direcionados durante a implementação; no fechamento, gates integrais do projeto, Vitest, Playwright contra AppHost e WCAG 2.1 AA, com resultados no progress. Manter isolate=true no Vitest.

O build Angular aprovado conserva um aviso: bundle inicial de 513,79 kB para orçamento de aviso de 500 kB. O limite permanece inalterado. Os resultados finais, incluindo cobertura, contrato e duas rodadas E2E, ficam registrados no progress.
