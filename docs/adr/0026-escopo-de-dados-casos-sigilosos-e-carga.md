# ADR 0026 — Escopo de dados dos processos, casos sigilosos e teste de carga

- **Status:** Aceita (PR #25 mergeado em 2026-10-18)
- **Data:** 2026-10-18
- **Etapa:** 5 (PR 5B)

## Contexto

A seção 7.2 pede a segunda etapa da autorização: o processo precisa estar no escopo da atribuição de perfil (próprio, equipe, área, filial, escritório). A seção 6.3 pede casos sigilosos (muralha ética): o caso com restrição só fica visível para a lista, e toda negativa vira `AccessDenied` na auditoria. O critério 1 da Etapa 5 pede a lista com 100 mil processos no tenant respondendo em menos de 1,5 s (p95), com filtro por área e responsável, medida com k6.

Decisões de negócio confirmadas em 2026-10-18:
- a restrição tem duas listas, **permitidos** e **bloqueados**, cada entrada sendo um usuário ou uma equipe;
- estar na lista de permitidos **basta**, mesmo fora do escopo;
- quem tem `cases.restrictions.manage` **vê** os casos sigilosos para poder gerenciar, **salvo se estiver bloqueado**;
- o teste de carga roda com **k6 no Docker contra o AppHost**, com login pelo fluxo real.

## Decisão

1. **Regra de alcance** (`CaseAccess`), para a permissão do endpoint (`cases.read`, `cases.edit`, `cases.delete`, `cases.restrictions.manage`):
   1. bloqueado (o próprio usuário ou uma equipe de que é membro): nunca alcança;
   2. caso com permitidos: alcança quem está na lista (diretamente ou por equipe) e quem gerencia restrições;
   3. caso sem lista, vale o escopo:
      - escritório: todos;
      - próprio: o usuário é responsável;
      - equipe X: a equipe X é responsável, ou um membro dela é responsável;
      - área: a área do processo;
      - filial: a filial do processo;
      - vários escopos se somam.
   Tudo vira um único filtro LINQ, traduzido em `EXISTS` no PostgreSQL. A lista usa o mesmo filtro, então nunca mostra o que o detalhe recusaria.
2. **Autorizadores de recurso** (`IResourceAuthorizer`):
   - um por caso de uso de processo: detalhe, edição, exclusão, encerramento, situação, relações (os dois processos) e restrições;
   - fora do alcance a resposta é **404**, como se o caso não existisse;
   - se o caso negado for sigiloso, grava `AccessDenied` em `audit.audit_logs`, na hora e fora da transação, que nem chega a abrir.
3. **Restrições** (`litigation.case_restrictions`, com RLS):
   - `GET` e `PUT /api/v1/cases/{id}/restrictions`, com `cases.restrictions.manage`;
   - usuários precisam ser membros e equipes precisam ser do escritório;
   - ninguém aparece duas vezes;
   - listas vazias tiram o sigilo;
   - a resposta do processo traz `isRestricted`.
4. **Dados ligados a processos em outros módulos:**
   - `ICaseVisibility.HiddenCaseIdsAsync` (Litigation.Contracts) devolve os casos sigilosos que o usuário não vê com `cases.read`;
   - o "segundo filtro global" da seção 6.3 nas entidades `ICaseScoped` fica para o primeiro módulo que tiver esses dados fora do Litigation (prazos, na Etapa 7). Esse módulo aplica o contrato. Hoje as únicas entidades `ICaseScoped` são as filhas do próprio processo, que já seguem o acesso a ele;
   - o conflito de interesses continua vendo as partes de todos os processos: é uma checagem do escritório inteiro, que não mostra o conteúdo do caso.
5. **Lista:**
   - filtros por situação, tipo, área, filial, fase, responsável, equipe e pessoa (parte), além da busca por pasta ou CNJ;
   - índices novos `(organization_id, practice_area_id, created_at)` e `(organization_id, branch_id, created_at)`.
6. **Chaves geradas pela aplicação:**
   - as entidades filhas recebem o id no domínio (`Guid.CreateVersion7()`), mas o EF trata chaves Guid como geradas na inserção. Com isso, uma filha nova numa edição era tratada como alteração e a gravação falhava;
   - o caso apareceu nas restrições e afetava também partes, responsáveis, endereços, contatos e itens de proposta;
   - essas chaves passaram a ser `ValueGeneratedNever`. É uma mudança só de modelo, sem SQL, com uma migration no Crm e outra no Litigation;
   - testes de regressão cobrem a edição de pessoa, proposta e processo com filhas novas.
7. **Teste de carga** (`tests/LexFlow.LoadTests`):
   - k6 1.0.0 pela imagem oficial `grafana/k6`, licença AGPL-3.0. É ferramenta de desenvolvimento, não vai para o produto e não é dependência do código;
   - `prepare-load.spec.ts` (Playwright) cria a conta e o escritório pelo fluxo real e guarda o token;
   - `seed-cases.sql` insere os processos com `generate_series`:
     - metade em cada área;
     - um em cada cinco com o usuário da carga como responsável;
     - 1% sigilosos;
   - o k6 manda o cabeçalho `Host` original, porque o OpenIddict confere o emissor pelo host da requisição.

## Consequências

- **Critério 1 da Etapa 5 atendido localmente:**
  - cenário: 100 mil processos, 10 usuários simultâneos por 60 s, cerca de 133 requisições por segundo;
  - p95 de 91 ms com filtro por área e responsável, e de 89 ms só com área;
  - nenhuma falha.
  O ambiente de produção precisa repetir a medição; a meta da Etapa 13 (500 mil processos) fica para lá.
- **Custo do escopo de equipe:** ele busca no Organization os usuários das equipes a cada requisição. Se pesar com equipes grandes, entra cache junto com o das permissões.
- **Dados do teste de carga:** o seed fica no banco do AppHost (volume persistente), em escritórios próprios da carga.
