# ADR 0030 — Telas de processos, preferências do usuário e opções de cadastro

- **Status:** Proposta (para revisão no PR 5E1)
- **Data:** 2026-10-18
- **Etapa:** 5 (PR 5E1)

## Contexto

A Etapa 5 pede (tarefa 9):
- a lista de processos com filtros salvos e colunas configuráveis;
- o assistente de cadastro;
- o detalhe com abas (Resumo, Partes, Pedidos e valores, Risco, Audiências, Acordos, Linha do tempo, Documentos, Prazos, Financeiro);
- a tela de importação.

O 5E foi dividido em 2026-10-18:
- **5E1:** lista, assistente, detalhe básico, lote, linha do tempo, sigilo e numeração;
- **5E2:** pedidos, risco, acordos, audiências e importação.

Decisão de negócio de 2026-10-18: os filtros salvos e as colunas ficam **no servidor, por usuário**.

## Decisão

1. **Preferências de tela (`platform.user_preferences`, com RLS):**
   - `GET /api/v1/me/preferences?key=` (`identity.profile.read`) e `PUT /api/v1/me/preferences` (`identity.profile.manage`);
   - a chave usa minúsculas, números, `.`, `-` e `_` (até 100 caracteres);
   - o valor é um objeto ou uma lista JSON de até 16 KB, que só a tela entende;
   - um registro por usuário, escritório e chave; cada usuário só lê e grava as dele;
   - a chave vai na query, então a rota não tem parâmetro e não precisa de caso entre organizações;
   - a lista de processos usa a chave `cases.list`: `{ columns, saved: [{ name, filters }] }`.
2. **Opções de cadastro (`GET /api/v1/cases/options`, `cases.read`):**
   - devolve áreas, filiais, equipes, membros (nome e e-mail) e os itens ativos das tabelas auxiliares dos processos (fases, tipos de ação, motivos de encerramento, objetos dos pedidos), só com id e nome;
   - motivo: as listas da estrutura e dos membros exigem `settings.structure.manage` e `settings.users.read`, e quem cadastra processos (advogado, estagiário) não tem essas permissões.
3. **Correção do contrato:**
   - o `AssignmentResponse` do Litigation (papel, usuário, equipe) tinha o mesmo nome do `AssignmentResponse` do Organization (perfil, escopo). No OpenAPI um sobrescrevia o outro, e desde o 5A o `assignments` do processo aparecia com o esquema errado no cliente gerado;
   - o do Litigation passou a se chamar `CaseAssignmentResponse`. O JSON devolvido pela API não mudou, só a descrição;
   - o oasdiff compara com a `main` e acusa a troca do esquema como quebra: a descrição antiga tinha campos obrigatórios que a API nunca devolveu. A exceção fica registrada em `docs/openapi/oasdiff-ignore.txt`, com esta ADR como justificativa.
4. **Frontend (`features/cases`):**
   - `CasesApi` é o único ponto de HTTP; as stores usam signals;
   - menu "Processos" (`cases.read`) e as rotas `/processos`, `/processos/novo`, `/processos/:id` e `/processos/:id/editar`;
   - **lista:** busca, filtros, filtros salvos, colunas, "carregar mais" e lote sobre os selecionados (o resultado mostra os que ficaram de fora e o motivo);
   - **assistente** em passos (Material stepper):
     1. tipo e CNJ, com a consulta do tribunal;
     2. dados;
     3. partes, com busca e cadastro rápido no Crm;
     4. responsáveis;
     5. revisão;
   - **detalhe:**
     - ações: editar, suspender, reativar, encerrar, reabrir e excluir, com confirmação;
     - abas Resumo, Partes e responsáveis, Hierarquia, Linha do tempo (com filtro por tipo e textos traduzidos a partir do código do acontecimento) e Sigilo (só para quem gerencia restrições);
     - Documentos, Prazos e Financeiro ficam reservados;
   - **configurações:** aba "Numeração de processos" (`settings.organization.manage`).

## Consequências

- O e2e `cases-flow.spec.ts` (contra o AppHost) percorre o assistente, a suspensão, a linha do tempo, o filtro salvo (que continua depois de recarregar) e o lote, com o axe em cada tela.
- As preferências servem a outras telas: basta uma chave nova (ex.: `people.list`).
- **Ficam para o 5E2:** as abas Pedidos e valores, Risco, Acordos e Audiências e a tela de importação.
