# ADR 0003 — Projetos de módulo criados na etapa que os implementa

- **Status:** Aceita (revisão humana em 2026-10-16)
- **Data:** 2026-10-16
- **Etapa:** 0

## Contexto

A seção 4.1 lista 15 módulos de negócio, cada um com 5 projetos. A tarefa 1 da Etapa 0 pede "a árvore da seção 4.1". Criar agora uns 75 projetos vazios deixaria o build mais lento, encheria a solução e geraria referências a código inexistente, sem verificar nada.

## Decisão

- Na Etapa 0 são criados os BuildingBlocks (`SharedKernel`, `Application.Abstractions`, `Infrastructure.Common`, `Web.Common`), os hosts (`Api`, `Worker`), o Aspire (`AppHost`, `ServiceDefaults`), o frontend e os projetos de teste.
- Cada módulo (`src/Modules/<Modulo>/LexFlow.<Modulo>.{Domain,Application,Infrastructure,Contracts,Endpoints}`) é criado na etapa que o implementa: Identity na Etapa 2, Organization na 3 e assim por diante.
- Os testes de arquitetura descobrem os módulos pelo namespace, então cada módulo novo passa a ser verificado assim que é referenciado pelos hosts, sem alterar os testes.

## Consequências

- A árvore final é a mesma da seção 4.1. Só muda o momento em que cada pasta aparece.
- Se a revisão preferir a árvore completa desde já, basta gerar os projetos vazios. Os testes continuam válidos.
