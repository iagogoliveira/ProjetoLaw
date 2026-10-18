# LexFlow — Progresso da implementação

Ordem das etapas: `docs/spec/implementacao.md`, seções 9 a 12.

| Etapa | Status | Branch |
| --- | --- | --- |
| 0 — Bootstrap do repositório | **Concluída** (PR #3 mergeado) | `etapa-0-bootstrap` |
| 1 — Building blocks | **Concluída** (PRs #4, #5 e #6 mergeados; CI do `main` verde) | `etapa-1a-shared-kernel`, `etapa-1b-application-web`, `etapa-1c-infra-platform` |
| 2 — Identidade e autenticação | **Concluída** (PRs #7, #8, #9 e #10 mergeados) | `etapa-2a-identity-accounts`, `etapa-2b-oidc-login`, `etapa-2b2-mfa-me`, `etapa-2c-frontend-e2e` |
| 3 — Organizações, estrutura e permissões | **Concluída** (PRs #11 a #17 mergeados; os 4 critérios de aceite atendidos) | `etapa-3a-organizations`, `etapa-3b-tenant-permissions`, `etapa-3c-rls-security-tests`, `etapa-3d-invites-structure`, `etapa-3e-custom-fields`, `etapa-3f-settings-screens`, `etapa-3f2-settings-screens` |
| 4 — Pessoas e CRM | **Concluída** (PRs #18 a #23 mergeados; os 3 critérios de aceite atendidos, o de processos com processos falsos até a Etapa 5) | `etapa-4-pessoas-crm`, `etapa-4b-conflitos`, `etapa-4c-funil-propostas`, `etapa-4d-frontend-crm`, `etapa-4d2-funil-telas`, `etapa-4e-aprovacao-escritorio-solo` |
| 5 — Processos e casos (contencioso) | **Em andamento** (5A pronto para revisão; faltam 5B a 5E) | `etapa-5a-processos` |

---

## Etapa 5 — Processos e casos (em andamento; plano aprovado em 2026-10-18)

**RFs:** RF-09 (processos e casos), RF-13 (pedidos, valores e cálculos), RF-14 (risco básico) e RF-16 (acordos, estrutura).

**Decisões de negócio (2026-10-18):**
- A numeração interna conta a sequência por área e ano (`TRAB-2026-00001`); o escritório pode trocar o formato.
- Os índices monetários (IPCA-E, INPC, SELIC, TR) vêm das APIs públicas do BCB (SGS) e do IBGE (SIDRA), com fonte falsa em dev e nos testes.
- O cálculo da etapa é correção pelo índice mais juros simples em % ao mês, a partir de uma data. A regra da Lei 14.905/2024 fica para os cálculos completos.
- O processo tem um campo filial opcional; quem tem escopo de filial vê os processos dela.

**Plano (5 PRs):**
- **5A — módulo Litigation e cadastro:** processo e caso, CNJ e tribunal, numeração, partes, responsáveis, hierarquia, encerramento, `ILitigationLookup` real e fusão de cadastros.
- **5B — acesso e lista:** escopo de dados (próprio, equipe, área, filial, escritório), casos sigilosos (muralha ética), filtros e o teste de carga k6 com 100 mil processos.
- **5C — pedidos, valores, risco, acordos e audiências:** índices monetários com importação mensal e `IMonetaryCorrection` conferido com 10 cálculos.
- **5D — linha do tempo, importação XLSX/CSV e operações em lote.**
- **5E — frontend e e2e.**

**Critérios de aceite da Etapa 5:**

| Critério | Status | Evidência |
| --- | --- | --- |
| Lista com 100 mil processos no tenant responde em < 1,5 s (p95) com filtro por área e responsável | Pendente (5B) | |
| Importação de 10 mil linhas com 3% inválidas conclui e gera relatório exato das inválidas | Pendente (5D) | |
| Correção monetária confere com 10 casos calculados manualmente | Pendente (5C) | |

### PR 5A — módulo Litigation e cadastro de processos (pronto para revisão)

| # | Subtarefa | Testes |
| --- | --- | --- |
| 5.1 | Módulo `Litigation` (schema `litigation`, RLS nas 6 tabelas): processo judicial (CNJ com dígito verificador, único no escritório) e casos administrativos e extrajudiciais, com tribunal, instância, comarca, vara, rito, classe, assunto, tipo de ação, área, filial, fase, situação, valor e datas | `LegalCaseTests`, `LitigationDatabaseTests.Migrations_apply_from_scratch_and_revert`, `The_cnj_is_checked_unique_in_the_office_and_described_by_the_lookup` |
| 5.2 | Numeração interna configurável, com contador atômico por escritório, área e ano | `CaseNumberingTests` (4 casos), `The_counter_runs_per_office_area_and_year_and_a_rollback_gives_the_number_back`, `Concurrent_registrations_never_get_the_same_number`, `The_office_numbering_format_and_area_codes_are_used_from_then_on` |
| 5.3 | Tribunal e segmento pelo número CNJ (`ICourtDirectory`), no cadastro e na consulta `cnj-lookup` | `A_judicial_case_gets_the_court_from_the_cnj_and_a_folder_numbered_by_area_and_year` |
| 5.4 | Partes (pessoas do Crm, polo, cliente, advogado contrário com OAB), cliente pagador e responsáveis (sócio, advogado, equipe), conferidos nos outros módulos por contratos | `Parties_and_responsibles_must_belong_to_the_office`, `Parties_need_a_client_without_repeating_a_person_and_the_payer_is_a_client` |
| 5.5 | Hierarquia (principal, incidente, recurso, cumprimento, apenso), sem ciclo e com um principal por processo | `The_hierarchy_links_incidents_and_appeals_without_cycles` |
| 5.6 | Encerramento com motivo, resultado e valor final; reabertura; suspensão | `A_closed_case_keeps_the_outcome_until_it_is_reopened`, `A_closed_case_does_not_change_until_reopened` |
| 5.7 | `ILitigationLookup` real no conflito de interesses e fusão de cadastros do Crm refletida nas partes | `The_conflict_check_finds_an_opponent_in_a_case_of_another_client`, `Merging_people_in_the_crm_points_the_parties_to_the_kept_record` |
| 5.8 | Endpoints com `cases.*`, casos entre organizações nas 8 rotas novas com id, OpenAPI (9 caminhos novos, só adições) e cliente TypeScript | `EndpointSecurityTests`, `Every_tenant_table_has_forced_row_level_security_with_the_tenant_policy`, `OpenApiContractTests` |

**ADR:** [0025 — Módulo Litigation: processos e casos, numeração interna e partes do Crm](adr/0025-modulo-litigation-processos-e-numeracao.md) (proposta, para revisão no PR).

**Critério de aceite da Etapa 4 com processos reais:** "conflito detecta parte contrária cadastrada em processo de outro cliente" ✅ (`CaseTests.The_conflict_check_finds_an_opponent_in_a_case_of_another_client`).

**Última verificação local (PR 5A):** `dotnet build` Release (0 warnings), `dotnet format --verify-no-changes`, 607 testes .NET em 15 projetos e 14 do AppHost, 66 Vitest, `npm run lint`, `npm run build`, sem pacotes vulneráveis. Cobertura: Litigation.Application 90,5% e Litigation.Domain 98,7%. Sem mudança de modelo pendente nas migrations do Platform, Organization, Crm e Litigation. Sem mudança de tela (só o cliente TypeScript regenerado; Playwright não rodado de novo).

---

## Etapa 4 — Pessoas e CRM (concluída em 2026-10-18)

**RFs:** RF-06 (pessoas, contatos e partes), RF-07 (conflito de interesses) e RF-08 (prospecção, propostas e contratação).

**Decisões de negócio (2026-10-17):**
- A consulta de CNPJ usa a BrasilAPI, atrás de `ICnpjLookup` (troca de provedor sem mexer no módulo). Se ela estiver fora do ar, o cadastro continua manual.
- O conflito indireto cobre três casos: mesmo grupo econômico, nome parecido (busca aproximada, com a semelhança exibida) e contato compartilhado com uma parte já cadastrada.
- O critério "conflito detecta parte contrária em processo de outro cliente" usa `ILitigationLookup`. Nesta etapa a interface é um stub, testado com dados falsos; o teste com processos reais fecha na Etapa 5.

**Plano, em 4 PRs sequenciais:**
- **4A — cadastro de pessoas:**
  - módulo `Crm` (schema `crm`, RLS): `Person` (PF ou PJ, CPF ou CNPJ único no escritório), `PersonRole`, `Address`, `Contact`, `EconomicGroup` e dados fiscais;
  - campos customizados (`custom_fields jsonb` validado pelo `ICustomFieldValidator`);
  - busca com `unaccent` e `pg_trgm` sobre o nome normalizado;
  - `ICnpjLookup` com BrasilAPI e implementação falsa;
  - permissões `crm.people.*`.
- **4B — conflito, fusão e LGPD:**
  - `ConflictCheck` e `ConflictCheckHit` (direto, indireto ou nenhum, com justificativa obrigatória para prosseguir com alerta);
  - `ILitigationLookup` como stub e relatório das verificações;
  - fusão de duplicados reatribuindo vínculos, auditada;
  - anonimização LGPD que mantém a integridade referencial.
- **4C — funil e propostas:**
  - `Opportunity` no funil lead → reunião → proposta → aceita ou perdida, com origem e motivo de perda;
  - `Proposal` e `ProposalItem`, com DOCX por mesclagem simples de modelo (pacote OpenXml, que precisa de ADR e checagem de licença);
  - proposta aceita vira cliente e publica `ProposalAccepted` (o contrato de honorários entra na Etapa 10).
- **4D — frontend e e2e** (dividido em 2026-10-18):
  - **4D1:** telas de pessoas (lista, cadastro, detalhe com abas, unificar e anonimizar) e de conflito (verificação e relatório), com o e2e dos critérios de aceite;
  - **4D2:** kanban do funil, oportunidade, propostas com o fluxo de aprovação, modelos de proposta nas configurações e o e2e do funil até o aceite.
- **Escopo original do 4D:**
  - lista e detalhe de pessoas (abas dados, contatos, processos, documentos, financeiro; as três últimas como espaço reservado até as etapas delas);
  - tela de conflito e kanban do funil;
  - e2e dos critérios de aceite.

### PR 4E — aprovação da própria proposta em escritório de uma pessoa só (concluído: PR #23 mergeado em 2026-10-18)

**Decisão de negócio (2026-10-18):** o escritório com um único usuário pode aprovar a própria proposta. Pela hierarquia, esse usuário é o de maior permissão.

| # | Subtarefa | Testes |
| --- | --- | --- |
| 4.35 | `Proposal.Approve` aceita a autoaprovação quando o escritório tem um único membro ativo; com mais de um, a recusa continua (`crm.proposal_self_approval`) | `The_only_member_of_the_office_approves_the_own_proposal`, `The_author_cannot_approve_and_only_an_approved_proposal_is_sent` |
| 4.36 | Contrato `IOrganizationMembers` (contagem dos membros ativos da organização ativa), implementado pelo Organization e usado no caso de uso da aprovação | `The_only_member_approves_the_own_proposal_until_someone_else_joins` |
| 4.37 | E2E do funil: a administradora sozinha aprova a própria proposta; depois de o sócio entrar, ele aprova a revisada pela fila e o fluxo segue até o aceite | `e2e/funnel-flow.spec.ts` |

**Última verificação local (PR 4E):** `dotnet build` Release (0 warnings), `dotnet format --verify-no-changes`, 583 testes .NET em 13 projetos e 14 do AppHost, 66 Vitest, `npm run lint`, `npm run build`, Playwright contra o AppHost (11 aprovados, duas execuções seguidas) e contra o `ng serve` (4 aprovados; os 7 que exigem o AppHost são pulados). Cobertura: Crm.Application 93,7%, Crm.Domain 98,6% e Organization.Application 95,9%. Sem pacote novo e sem mudança no banco nem no contrato OpenAPI.

### PR 4D2 — telas do funil, das propostas e dos modelos (concluído: PR #22 mergeado em 2026-10-18)

| # | Subtarefa | Testes |
| --- | --- | --- |
| 4.29 | `features/funnel` com `FunnelApi` (único ponto HTTP) e stores em signals; menu "Funil comercial" (`crm.opportunities.read`) e rotas protegidas por permissão | `funnel-stores.spec.ts` |
| 4.30 | Kanban: colunas por etapa com quantidade e valor estimado, arrastar e soltar (CDK) entre lead, reunião e proposta, com o seletor "Mover para" como alternativa pelo teclado; o cartão volta se o servidor recusar; fila de propostas aguardando aprovação para quem aprova | `FunnelStore` (3 casos), `e2e/funnel-flow.spec.ts` |
| 4.31 | Oportunidade: cadastro com interessado solto ou pessoa cadastrada, origem e motivo de perda das tabelas auxiliares, responsável, campos customizados; detalhe com perder, reabrir, excluir, verificação de conflito com "prosseguir" e a lista de propostas | `OpportunityStore` (3 casos), `e2e/funnel-flow.spec.ts` |
| 4.32 | Proposta: editor do rascunho com itens por modalidade e total sem o êxito; fluxo enviar para aprovação → aprovar → registrar envio → aceite ou recusa, devolver e excluir; download do DOCX; atalho para a verificação de conflito quando o aceite é recusado | `ProposalStore` (3 casos), `e2e/funnel-flow.spec.ts` (autoaprovação recusada, aprovação pelo sócio convidado, aceite que cria o cliente) |
| 4.33 | Aba "Modelos de proposta" nas configurações: modelos do escritório, marcadores aceitos, upload, exclusão e o modelo padrão para baixar | `TemplatesStore` (1 caso), `e2e/funnel-flow.spec.ts` |
| 4.34 | Estilos de página e formatos brasileiros (CPF/CNPJ/CEP) movidos para `shared/`; downloads como `Blob` sobre o HttpClient do Angular; textos pt-BR e en; WCAG 2.1 AA (o quadro do kanban, com rolagem, é uma região focável) | `translations.spec.ts`, `expectAccessible` no e2e |

**Última verificação local (PR 4D2):** `dotnet build` Release (0 warnings), `dotnet format --verify-no-changes`, 581 testes .NET em 13 projetos e 14 do AppHost (sem mudança no backend), 66 Vitest, `npm run lint`, `npm run build`, Playwright contra o AppHost (11 aprovados, duas execuções seguidas) e contra o `ng serve` (4 aprovados; os 7 que exigem o AppHost são pulados). Cobertura: Crm.Application 93,6% e Crm.Domain 98,6%. Sem pacote novo.

**Pendências do PR 4D2:**
1. Com o download como `Blob`, um erro da API nesse pedido (ex.: 404) chega sem o `code` e aparece como erro genérico.
2. O nome do responsável no cartão do kanban não aparece (a lista de membros exige `settings.users.read`); o detalhe mostra quando há a permissão.

### PR 4D1 — telas de pessoas e conflito (concluído: PR #21 mergeado em 2026-10-18)

| # | Subtarefa | Testes |
| --- | --- | --- |
| 4.23 | `features/people` com `CrmApi` (único ponto HTTP) e stores em signals; menu "Pessoas" (`crm.people.read`) e "Conflito de interesses" (`crm.conflicts.check`), com rotas protegidas por permissão | `people-store.spec.ts`, `conflict-store.spec.ts` |
| 4.24 | Lista de pessoas: busca sem acentos com espera entre teclas (só a última resposta conta), filtro por papel, "carregar mais" | `PeopleStore` (2 casos), `e2e/people-flow.spec.ts` |
| 4.25 | Cadastro e edição: física ou jurídica, consulta de CNPJ, papéis, contatos, endereços, dados fiscais, grupo econômico (com criação na hora), campos customizados e aviso de possíveis duplicatas | `PersonEditorStore` (4 casos), `e2e/people-flow.spec.ts` |
| 4.26 | Detalhe com as abas Dados, Contatos, Processos, Documentos e Financeiro (as três últimas como espaço reservado) e as ações editar, unificar duplicado, anonimizar (com motivo) e excluir, confirmadas antes; a API passa a devolver `anonymizedAt` (só adição no OpenAPI) | `PersonDetailStore` (2 casos) |
| 4.27 | Conflito: verificação por lado, resultado com risco e achados, prosseguir com justificativa obrigatória quando há alerta e relatório filtrado por risco | `ConflictStore` (3 casos), `e2e/people-flow.spec.ts` |
| 4.28 | Textos em pt-BR e en (Transloco), WCAG 2.1 AA (axe) nas telas novas | `translations.spec.ts`, `expectAccessible` no e2e |

**Critérios de aceite da Etapa 4 pela tela (e2e contra o AppHost):**

| Critério | Status | Evidência |
| --- | --- | --- |
| CPF/CNPJ duplicado no mesmo tenant é bloqueado; em tenants diferentes é permitido | ✅ | `e2e/people-flow.spec.ts` (o mesmo CNPJ recusado no escritório e aceito no segundo) |
| Busca por "Joao da Silva" encontra "João da Silva Ltda." | ✅ | `e2e/people-flow.spec.ts` |
| Conflito detecta parte contrária cadastrada em processo de outro cliente | ✅ com processos falsos (API) | `ConflictTests.An_opponent_in_a_case_of_another_client_is_a_direct_conflict`; na tela, o conflito direto com parte contrária cadastrada e a justificativa obrigatória (`e2e/people-flow.spec.ts`). A prova com processos reais fica para a Etapa 5 |

**Última verificação local (PR 4D1):** `dotnet build` Release (0 warnings), `dotnet format --verify-no-changes`, 581 testes .NET em 13 projetos e 14 do AppHost, 56 Vitest, `npm run lint`, `npm run build`, Playwright contra o AppHost (10 aprovados, a suíte rodou duas vezes seguidas) e contra o `ng serve` (4 aprovados; os 6 que exigem o AppHost são pulados). Cobertura: Crm.Application 93,6% e Crm.Domain 98,6%. Sem pacote novo; o modelo do banco não mudou (só a resposta da API).

### PR 4C — funil de oportunidades e propostas (concluído: PR #20 mergeado em 2026-10-18)

**Decisões de negócio (2026-10-18):**
- O lead é um conjunto de dados soltos ou uma pessoa já cadastrada; a pessoa só nasce no aceite da proposta.
- Há um modelo DOCX padrão do sistema, e o escritório pode enviar o próprio.
- A aprovação interna é sempre exigida, e quem elabora não aprova a própria proposta.
- Aceitar exige a verificação de conflito do lead já prosseguida.

| # | Subtarefa | Testes |
| --- | --- | --- |
| 4.15 | `Opportunity` no funil lead → reunião → proposta → aceita ou perdida, com origem e motivo de perda das tabelas auxiliares novas, responsável membro do escritório, kanban com cursor e resumo por etapa | `FunnelAndProposalTests.Losing_needs_a_loss_reason_and_the_kanban_counts_by_stage`, `An_opportunity_refuses_an_unknown_origin_or_a_responsible_from_outside_the_office`, `FunnelAndProposalTests` (unitários, 5 casos) |
| 4.16 | Verificação de conflito do lead ligada à oportunidade; mudar nome ou documento desliga a verificação | `Changing_the_prospect_name_or_document_unlinks_the_conflict_check`, `An_accepted_proposal_needs_approval_by_someone_else_and_a_cleared_conflict_and_creates_the_client` |
| 4.17 | `Proposal` e `ProposalItem` (fixo, mensal, por hora, êxito, por ato; êxito fora do total), aprovação interna sem autoaprovação, envio, recusa, validade e substituição das outras propostas | `The_author_cannot_approve_and_only_an_approved_proposal_is_sent`, `An_expired_proposal_is_neither_sent_nor_accepted`, `Accepting_one_proposal_supersedes_the_other_open_ones` |
| 4.18 | Aceite: cliente criado ou reaproveitado (mesmo documento) com o papel Cliente, oportunidade ganha e `ProposalAcceptedIntegrationEvent` | `An_accepted_proposal_needs_approval_by_someone_else_and_a_cleared_conflict_and_creates_the_client`, `Accepting_reuses_the_person_with_the_same_document_and_adds_the_client_role` |
| 4.19 | Modelos DOCX (Open XML SDK): marcadores, linha de itens repetida, modelo padrão válido no esquema do Word, upload com antivírus, formato e marcadores conferidos, DOCX congelado no envio | `ProposalDocumentTests` (7 casos), `An_office_template_is_scanned_checked_and_used_by_the_proposal`, `Infected_invalid_or_unknown_marker_templates_are_refused`, `The_sent_document_is_frozen_while_a_draft_is_rendered_on_demand` |
| 4.20 | Tabelas auxiliares "Origens de oportunidade" e "Motivos de perda" no onboarding e, por migration de dados, nos escritórios existentes | `Existing_offices_receive_the_opportunity_lookup_tables`, `Onboarding_creates_the_system_lookup_tables_once_with_suggested_items` |
| 4.21 | Fusão e anonimização levam junto as oportunidades ligadas | `Merging_and_anonymizing_a_person_carry_over_to_the_linked_opportunities` |
| 4.22 | RLS nas 4 tabelas novas, casos entre organizações nas 18 rotas novas com id, OpenAPI (19 caminhos novos, só adições) e cliente TypeScript | `Every_tenant_table_has_forced_row_level_security_with_the_tenant_policy`, `EndpointSecurityTests`, `OpenApiContractTests` |

**ADR:** [0024 — Funil de oportunidades, propostas de honorários e modelos DOCX](adr/0024-funil-propostas-e-modelos-docx.md) (Aceita em 2026-10-18, com o merge do PR #20). O pacote novo `DocumentFormat.OpenXml` 3.5.1 é MIT, e as dependências dele também.

**Última verificação local (PR 4C):** `dotnet build` Release (0 warnings), `dotnet format --verify-no-changes`, 581 testes .NET em 13 projetos (os 14 do AppHost não rodaram de novo), 44 Vitest, `npm run lint`, `npm run build`, Playwright contra o `ng serve` (4 aprovados; os 4 fluxos que exigem o AppHost foram pulados; sem mudança de tela), sem pacotes vulneráveis. Cobertura: Crm.Application 93,6% e Crm.Domain 98,6%. Sem mudança de modelo pendente nas migrations.

**Pendências do PR 4C:**
1. **Escritório de uma pessoa só:** decidido em 2026-10-18 e implementado no PR 4E.
2. **Indicadores do RF-08** (conversão, tempo de fechamento, receita por origem): ficam para os relatórios. O funil já grava etapa, data da mudança, origem e valor.
3. **DOCX órfão:** se o envio falhar depois de gravar o arquivo, ele fica no armazenamento sem referência. A limpeza entra com o GED (Etapa 9).
4. **Assinatura eletrônica** da proposta (RF-29) fica para a Etapa 9.

### PR 4B — conflito de interesses, fusão e LGPD (concluído: PR #19 mergeado em 2026-10-18)

**Decisões de negócio (2026-10-17):**
- A consulta de conflito informa o lado: a pessoa vai ser cliente ou parte contrária.
- Na fusão, o cadastro mantido fica com os próprios dados, e os campos vazios recebem os do duplicado.
- A anonimização usa a permissão nova `crm.people.anonymize` (só Administrador do escritório e Sócio) e exige motivo.

| # | Subtarefa | Testes |
| --- | --- | --- |
| 4.8 | Verificação de conflito: direto (documento, nome ou parte em processo de outro cliente) e indireto (nome parecido, grupo econômico, e-mail compartilhado), conforme o lado | `ConflictTests` (6 casos), `ConflictAndLifecycleTests.A_check_takes_the_highest_risk_of_its_hits` |
| 4.9 | Prosseguir com justificativa obrigatória quando há risco; relatório das verificações por risco e cursor | `A_future_client_who_is_already_an_opponent_is_a_direct_conflict_that_needs_a_justification`, `The_report_lists_checks_by_risk_newest_first` |
| 4.10 | `ILitigationLookup` como stub até a Etapa 5, com o critério de aceite provado por uma implementação falsa | `An_opponent_in_a_case_of_another_client_is_a_direct_conflict` |
| 4.11 | Fusão de duplicados: vazios completados, papéis, endereços, contatos e campos somados, achados de conflito reatribuídos, evento `PeopleMergedIntegrationEvent` | `Merging_moves_data_and_conflict_hits_to_the_kept_record`, `Merging_keeps_the_record_fills_the_blanks_and_sums_roles_addresses_and_contacts` |
| 4.12 | Anonimização LGPD com motivo, também nos achados e consultas de conflito; cadastro congelado; permissão nova com migration de dados | `Anonymizing_removes_personal_data_everywhere_and_freezes_the_record`, `Existing_admin_and_partner_roles_receive_the_anonymize_permission` |
| 4.13 | Filtros `?role=` e `?risk=` sem diferenciar maiúsculas (400 `crm.filter_invalid`), com o contrato OpenAPI mantido no schema do enum | `The_list_filters_by_role_written_in_camel_case`, `OpenApiContractTests` |
| 4.14 | RLS nas 2 tabelas novas, casos entre organizações nas 4 rotas novas com id, OpenAPI (6 operações novas, só adições) e cliente TypeScript | `Every_tenant_table_has_forced_row_level_security_with_the_tenant_policy`, `EndpointSecurityTests` |

**ADR:** [0023 — Conflito de interesses, fusão de cadastros e anonimização LGPD](adr/0023-conflito-fusao-e-anonimizacao.md) (Aceita em 2026-10-18, com o merge do PR #19).

**Critério de aceite da Etapa 4:** "conflito detecta parte contrária cadastrada em processo de outro cliente" ✅ com processos falsos (`ConflictTests.An_opponent_in_a_case_of_another_client_is_a_direct_conflict`). A prova com processos reais fica para a Etapa 5.

**Última verificação local (PR 4B):** `dotnet build` (0 warnings), `dotnet format --verify-no-changes`, 553 testes .NET em 13 projetos e 14 do AppHost, 44 Vitest, `npm run lint`, `npm run build`, sem pacotes vulneráveis. Cobertura: Crm.Application 96,6% e Crm.Domain 100%. Sem mudança de modelo pendente nas migrations. Sem mudança de tela.

**CI do PR 4B:** os testes do ClamAV falharam com "Broken pipe": o freshclam atualizava as assinaturas na partida e o clamd recarregava no meio do teste. O container de teste agora roda sem freshclam e espera o `clamdcheck.sh`, não só a porta.

**Pendências do PR 4B:**
1. LGPD × auditoria: a trilha só de inserção guarda os valores anteriores; o prazo de retenção e o tratamento dela para titulares é decisão jurídica antes de produção.
2. O contato compartilhado compara só e-mails; telefones exigem normalização (entra quando houver demanda).

### PR 4A — cadastro de pessoas (concluído: PR #18 mergeado em 2026-10-17)

| # | Subtarefa | Testes |
| --- | --- | --- |
| 4.1 | Módulo `Crm` (schema `crm`, RLS nas 5 tabelas): pessoa física ou jurídica, papéis, endereços (CEP e UF), contatos (área, canal), dados fiscais e grupos econômicos | `PersonTests`, `CrmDatabaseTests.Migrations_apply_from_scratch_and_revert`, `Every_tenant_table_has_forced_row_level_security_with_the_tenant_policy` |
| 4.2 | Documento conferido pelo tipo e único no escritório (índice parcial e 409 `crm.document_taken`) | `A_document_is_unique_inside_the_organization_only`, `PeopleTests.A_duplicate_document_is_refused_in_the_same_office_and_allowed_in_another` |
| 4.3 | Busca sem acentos e aproximada (`word_similarity` do pg_trgm, índice GIN), por documento, e lista alfabética com cursor | `Word_similarity_finds_a_name_without_accents_inside_a_longer_name`, `Searching_without_accents_finds_the_longer_name_and_documents_are_searchable` |
| 4.4 | Possíveis duplicatas (mesmo documento ou nome parecido) | `Similar_names_and_documents_are_reported_as_possible_duplicates` |
| 4.5 | Campos customizados de pessoas validados pelo `ICustomFieldValidator` | `Custom_fields_of_people_are_validated_and_stored` |
| 4.6 | Consulta de CNPJ (`ICnpjLookup`: BrasilAPI, falsa nos testes) | `The_cnpj_lookup_fills_in_the_company_or_explains_why_not` |
| 4.7 | Endpoints com permissões `crm.people.*`, casos entre organizações nos SecurityTests, OpenAPI (11 operações novas, só adições) e cliente TypeScript | `EndpointSecurityTests`, `OpenApiContractTests` |

**ADR:** [0022 — Módulo Crm: cadastro de pessoas, busca aproximada e consulta de CNPJ](adr/0022-modulo-crm-pessoas-busca-e-cnpj.md) (Aceita em 2026-10-17, com o merge do PR #18).

**Critérios de aceite da Etapa 4 que fecham aqui:**

| Critério | Status | Evidência |
| --- | --- | --- |
| CPF/CNPJ duplicado no mesmo tenant é bloqueado; em tenants diferentes é permitido | ✅ | `PeopleTests.A_duplicate_document_is_refused_in_the_same_office_and_allowed_in_another`, `CrmDatabaseTests.A_document_is_unique_inside_the_organization_only` |
| Busca por "Joao da Silva" encontra "João da Silva Ltda." | ✅ | `PeopleTests.Searching_without_accents_finds_the_longer_name_and_documents_are_searchable` |

**Última verificação local (PR 4A):** `dotnet build` (0 warnings), `dotnet format --verify-no-changes`, 538 testes .NET em 13 projetos (Testcontainers; API e SecurityTests no login da aplicação) e 14 do AppHost, 44 Vitest, `npm run lint`, `npm run build`, sem pacotes vulneráveis. Cobertura: Crm.Application 93,5% e Crm.Domain 100%. As migrations dos quatro módulos não têm mudança de modelo pendente. Sem mudança de tela (Playwright não rodado de novo).

**Pendências do PR 4A:**
1. O índice GIN de trigramas não começa por `organization_id`. Reavaliar `btree_gin` (ou o operador `%>` com índice) no teste de carga.
2. A exportação de pessoas (`crm.people.export`) ainda não tem endpoint.
3. A BrasilAPI não tem SLA: indisponível vira 422 e o cadastro segue manual.

---

## Etapa 3 — Organizações, estrutura e permissões (concluída em 2026-10-17)

**RFs:** RF-01 (convites, MFA obrigatório por organização), RF-02, RF-03 (campos customizados e tabelas auxiliares) e seção 3 da especificação. Revisão humana dos PRs obrigatória (seção 14.4).

**Critérios de aceite da Etapa 3:**

| Critério | Status | Evidência |
| --- | --- | --- |
| Usuário da Org A recebe 404 em qualquer recurso da Org B (100% dos endpoints) | ✅ (3C) | `EndpointSecurityTests.Resources_of_another_organization_or_user_answer_404` |
| SQL cru com `app.organization_id` da Org A não retorna linhas da Org B | ✅ (3C) | `RowLevelSecurityTests.Raw_sql_as_the_application_only_sees_the_organization_of_the_session` |
| Remover `settings.users.manage` de um perfil bloqueia a ação em até 5 s, sem novo login | ✅ (3B) | `AccessAndPermissionsTests.Removing_a_permission_from_a_role_blocks_the_action_within_5_seconds_without_a_new_login` |
| Campo customizado obrigatório criado na tela aparece e é validado no formulário de teste | ✅ (3F2) | `e2e/custom-fields-flow.spec.ts` (contra o AppHost) |

**Decisões de negócio confirmadas (2026-10-17):**
- O MFA obrigatório por organização entra na Etapa 3 (PR 3B): o escritório pode exigir segundo fator de todos os membros.
- A matriz de permissões dos perfis padrão da ADR 0017 foi aprovada.
- No onboarding são criados só os perfis internos da seção 3.2 (Administrador do escritório, Sócio, Advogado, Estagiário/paralegal, Controladoria jurídica e Financeiro). Cliente do portal e Correspondente ficam para a Etapa 12; Administrador da plataforma, para a Etapa 13.
- Convites (PR 3D): quem ainda não tem conta se cadastra pelo link, com o e-mail já preenchido; só a conta cujo e-mail confirmado é o convidado aceita; os perfis (com escopo de dados) são escolhidos no convite.

**Plano (proposto em 2026-10-17), em 6 PRs sequenciais:**
- **3A:** módulo Organization (schema `org`), `PermissionCatalog` completo e onboarding com perfis padrão. A matriz de permissões dos perfis vai numa ADR para aprovação.
- **3B:** `ITenantContext` real (`X-Organization-Id` validado contra `OrganizationMember`), troca de organização, `PermissionService` com cache Redis e invalidação por evento, `IResourceAuthorizer<T>`, MFA obrigatório por organização e permissões no `GET /api/v1/me`.
- **3C:** RLS e papéis do banco (incluindo `REVOKE UPDATE, DELETE` em `audit.audit_logs`), `LexFlow.SecurityTests` com descoberta automática de endpoints e autorização do painel do Hangfire.
- **3D:** convites (token de uso único, 7 dias), membros, estrutura (filiais, áreas, equipes, centros de custo, sócios) e perfis editáveis.
- **3E:** campos customizados e tabelas auxiliares (RF-03), com o componente `lf-custom-fields`.
- **3F:** telas de configurações, diretiva `lfCan` e e2e do onboarding (cadastro → criar escritório → convidar → aceitar).

### PR 3F — frontend da Etapa 3 (plano aprovado em 2026-10-17, em duas partes)

**3F1 — organização no SPA, convites e onboarding (concluído: PR #16 mergeado em 2026-10-17):**

| # | Subtarefa | Testes |
| --- | --- | --- |
| 3.29 | `AccessStore` (`GET /api/v1/me/access`), organização ativa na aba e `X-Organization-Id` só nas chamadas à API; organização que deixou de valer volta à escolha do servidor | `organization.spec.ts` (`AccessStore`, `organizationInterceptor`) |
| 3.30 | Troca de organização no shell; diretiva `*lfCan` e guards `organizationGuard` (sem escritório → onboarding) e `permissionGuard` | `organization.spec.ts` (`CanDirective`, guards) |
| 3.31 | Onboarding "Crie seu escritório" e tela mínima "Convidar pessoa" (perfil e escopo próprio ou escritório), que o 3F2 amplia | `e2e/onboarding-flow.spec.ts` |
| 3.32 | Página pública `/convite`: consulta, "Entrar" ou "Criar conta" com o e-mail preenchido, token guardado na aba e retomado depois do login, aceite e entrada no escritório | `invitation-store.spec.ts`, `e2e/onboarding-flow.spec.ts` |
| 3.33 | E2E do onboarding contra o AppHost: cadastro → criar escritório → convidar → a convidada cria a conta, confirma, entra e aceita | `e2e/onboarding-flow.spec.ts`; `e2e/account-flow.spec.ts` ajustado (sem escritório, o login leva ao onboarding) |

**Última verificação local (PR 3F1):** 42 Vitest, `npm run lint`, `npm run build`, Playwright 4 contra o `ng serve` (os 2 fluxos completos são pulados sem Mailpit) e 6 de 6 contra o AppHost, em duas rodadas. Este PR não muda código .NET; a suíte .NET é a verificada no 3E.

**Pendências do PR 3F1:**
1. O token do convite fica no `sessionStorage` da aba: se a confirmação do e-mail for aberta em outra aba ou navegador, a pessoa abre o link do convite de novo depois de entrar.
2. Quem precisa ativar o MFA exigido pelo escritório recebe 403 `organization.mfa_required` nas telas do escritório, mas ainda não há aviso dedicado no shell (entra no 3F2).

**3F2 — telas de configurações, que fecham a Etapa 3 (concluído: PR #17 mergeado em 2026-10-17):**

| # | Subtarefa | Testes |
| --- | --- | --- |
| 3.34 | `GET` e `PUT /api/v1/organization` (nome e política de MFA do escritório ativo, com `settings.organization.manage`) | `MemberAndRoleManagementTests.Office_admin_renames_the_office`, SecurityTests (descoberta automática) |
| 3.35 | `/configuracoes` com uma aba por tela, cada uma visível só com a permissão dela (`*lfCan`); link "Configurações" no shell e aviso quando o escritório exige MFA e a conta não tem segundo fator | `e2e/onboarding-flow.spec.ts`, `e2e/settings-flow.spec.ts` |
| 3.36 | Usuários e convites: membros com nome, e-mail e perfis por escopo (editar, remover); convites com situação, reenviar e cancelar | `e2e/onboarding-flow.spec.ts` |
| 3.37 | Perfis: criar, renomear, excluir e matriz de permissões por grupo (Administrador do escritório travado) | `e2e/settings-flow.spec.ts` |
| 3.38 | Estrutura: filiais, áreas, equipes (com membros), centros de custo e sócios, com os erros do servidor (ex.: item em uso) | `e2e/settings-flow.spec.ts` |
| 3.39 | Campos customizados por entidade (criar, obrigatório, ativo), tabelas auxiliares e itens, formulário de teste com `lf-custom-fields` e validação no servidor | `e2e/custom-fields-flow.spec.ts`, `custom-fields.spec.ts` |

**Critério de aceite da Etapa 3 que fecha aqui:** campo customizado obrigatório criado na tela aparece e é validado no formulário de teste ✅ (`e2e/custom-fields-flow.spec.ts`, contra o AppHost).

**Correções no `lf-custom-fields`:**
- O componente mantém um único `FormGroup` e sincroniza nele os controles (adiciona, remove e atualiza validadores). Trocar de grupo deixava o campo preso ao grupo antigo.
- Ele também se redesenha quando o formulário muda fora dos signals (envio, erros da API).

**Última verificação local (PR 3F2):** `dotnet build` (0 warnings), `dotnet format --verify-no-changes`, 519 testes .NET em 11 projetos e 14 do AppHost, 44 Vitest, `npm run lint`, `npm run build`, sem pacotes vulneráveis; Playwright 4 contra o `ng serve` (os 4 fluxos completos são pulados sem Mailpit) e 8 de 8 contra o AppHost, em duas rodadas. O OpenAPI só ganhou as 2 operações do escritório.

**Pendências do PR 3F2:**
1. As telas usam tabelas simples, sem paginação: bastam para a estrutura de um escritório, mas listas grandes (membros de escritórios grandes) vão precisar de paginação no servidor.
2. O escopo por equipe, área ou filial só aparece para quem também administra a estrutura (`settings.structure.manage`); é a mesma pendência de permissão de leitura da ADR 0020.

### PR 3E — campos customizados e tabelas auxiliares (concluído: PR #15 mergeado em 2026-10-17)

**Decisões de negócio (2026-10-17):**
- O campo do tipo lista tira as opções de uma tabela auxiliar do escritório.
- As 6 tabelas auxiliares (tipos de ação, fases, objetos/pedidos, motivos de encerramento, tipos de despesa, tipos de tarefa) nascem no onboarding com itens sugeridos, que vão para aprovação numa ADR.
- Nova permissão `settings.custom_fields.read`, dada a todos os perfis padrão (inclusive os já criados); editar continua com `settings.custom_fields.manage`.
- Entidades com campos customizados: processos e casos, pessoas e clientes, contratos de honorários e oportunidades.

| # | Subtarefa | Testes |
| --- | --- | --- |
| 3.23 | Definições de campos (texto, número, data, lista, moeda, usuário) por entidade, com chave, entidade e tipo fixos e desativação no lugar da exclusão | `CustomFieldTests` (unitários e da API) |
| 3.24 | `ICustomFieldValidator` (Platform.Contracts) com códigos `custom_fields.*` e `POST /api/v1/custom-fields/validate` | `Each_rule_reports_its_own_code`, `Required_field_created_by_the_office_is_enforced_on_validation` |
| 3.25 | Tabelas auxiliares e itens editáveis; as 6 do sistema nascem no onboarding (`OrganizationCreatedIntegrationEvent` → Worker), com itens sugeridos | `Onboarding_creates_the_system_lookup_tables_once_with_suggested_items`, `Office_maintains_lookup_tables_and_items_without_deleting_them` |
| 3.26 | Permissão `settings.custom_fields.read` em todos os perfis padrão, inclusive os existentes (migration de dados) | `Every_default_role_reads_the_fields_but_only_managers_change_them`, `Existing_default_roles_receive_the_custom_fields_read_permission` |
| 3.27 | RLS nas 3 tabelas novas, casos entre organizações nas 4 rotas novas com id, OpenAPI (9 operações novas) e cliente TypeScript | `Every_tenant_table_has_forced_row_level_security_with_the_tenant_policy`, `EndpointSecurityTests`, `OpenApiContractTests` |
| 3.28 | Componente `lf-custom-fields` (controles com os limites do servidor, opções ativas da tabela, erros traduzidos, inclusive os da API) | `custom-fields.spec.ts` |

**ADR:** [0021 — Campos customizados e tabelas auxiliares](adr/0021-campos-customizados-e-tabelas-auxiliares.md) (Aceita em 2026-10-17, com o merge do PR #15, incluindo os itens sugeridos).

**Última verificação local (PR 3E):** `dotnet build` (0 warnings), `dotnet format --verify-no-changes`, 519 testes .NET em 11 projetos (Testcontainers) e 14 do AppHost, 30 Vitest, `npm run lint`, `npm run build`, sem pacotes vulneráveis. Cobertura: Platform.Application 95,5% e Platform.Domain 98,8%. As migrations não têm mudança de modelo pendente; o OpenAPI só ganhou adições. O Playwright não foi rodado de novo (nenhuma tela mudou; o componente entra nas telas do 3F).

**Pendências do PR 3E:**
1. A tela de campos customizados e o e2e do critério de aceite (campo obrigatório criado na tela aparece e é validado no formulário de teste) entram no 3F.
2. Os módulos donos de entidade (Etapas 4, 5 e 10) criam a coluna `custom_fields jsonb`, chamam o `ICustomFieldValidator` e indexam (GIN) os campos filtráveis.

### PR 3D — convites, membros, estrutura e perfis editáveis (concluído: PR #14 mergeado em 2026-10-17)

| # | Subtarefa | Testes |
| --- | --- | --- |
| 3.17 | Convites por e-mail: token de uso único gerado pelo Worker (só o hash no banco), 7 dias, reenvio e cancelamento; consulta pública do convite e aceite pela conta do e-mail confirmado, com os perfis do convite | `InvitationTests` (API: aceite único, e-mail errado ou não confirmado, expiração e reenvio, cancelamento, duplicados), `InvitationTests` (domínio) |
| 3.18 | `IUserDirectory` (Identity.Contracts): membros com nome e e-mail; cadastro com `?email=` vindo do convite | `Invited_person_accepts_once_and_joins_with_the_roles_of_the_invitation` |
| 3.19 | Remoção de membro (acesso termina na próxima requisição; último administrador protegido) | `MemberAndRoleManagementTests.Removed_member_loses_access_and_the_last_admin_stays` |
| 3.20 | Estrutura: filiais (CNPJ), áreas, equipes e seus membros, centros de custo e sócios (soma até 100%), com exclusão barrada enquanto em uso | `StructureTests` |
| 3.21 | Perfis editáveis (criar, renomear, excluir sem uso) e catálogo para a matriz (`GET /api/v1/permissions`) | `MemberAndRoleManagementTests` |
| 3.22 | RLS nas tabelas novas, casos entre organizações para as 16 rotas novas com id, contrato OpenAPI (32 operações novas, só adições) e cliente TypeScript | `Every_tenant_table_has_forced_row_level_security_with_the_tenant_policy`, `EndpointSecurityTests`, `OpenApiContractTests` |

**ADR:** [0020 — Convites, membros, estrutura e perfis editáveis](adr/0020-convites-estrutura-e-perfis-editaveis.md) (Aceita em 2026-10-17, com o merge do PR #14).

**Última verificação local (PR 3D):** `dotnet build` (0 warnings), `dotnet format --verify-no-changes`, 501 testes .NET em 11 projetos (Testcontainers; API e SecurityTests no login da aplicação) e 14 do AppHost, 23 Vitest, Playwright 4 contra o `ng serve` (o fluxo completo é pulado sem Mailpit; não rodado contra o AppHost neste PR), `npm run lint`, `npm run build`, sem pacotes vulneráveis. Cobertura: Organization.Application 94,6% e Organization.Domain 100%. As migrations dos três módulos não têm mudança de modelo pendente; o OpenAPI só ganhou adições.

**Pendências do PR 3D:**
1. A leitura da estrutura exige `settings.structure.manage`. Quando os formulários de outros módulos precisarem escolher equipe ou área, será preciso uma permissão de leitura.
2. A página do convite no SPA (`/convite?token=…`: consulta, entrar ou criar conta com o e-mail preenchido, aceitar) entra no PR 3F, com o e2e do onboarding.

### PR 3C — RLS, papéis do banco e SecurityTests (concluído: PR #13 mergeado em 2026-10-17)

| # | Subtarefa | Testes |
| --- | --- | --- |
| 3.12 | Papel `lexflow_app` (sem `BYPASSRLS`, só DML) e login da aplicação criado pelo MigrationService; API e Worker conectam com ele (AppHost e testes) | `RowLevelSecurityTests.The_application_login_has_no_special_powers`, toda a suíte de integração da API rodando com o login |
| 3.13 | `app.organization_id` na sessão (`TenantSessionInterceptor`) e RLS forçada em toda tabela `ITenantOwned` | `Every_tenant_table_has_forced_row_level_security_with_the_tenant_policy`, `Raw_sql_as_the_application_only_sees_the_organization_of_the_session`, `The_application_cannot_write_rows_of_another_organization` |
| 3.14 | Auditoria só de inserção para a aplicação (`REVOKE UPDATE, DELETE, TRUNCATE`), leitura por organização, encadeamento como dono | `The_audit_trail_is_append_only_for_the_application` |
| 3.15 | Limpeza de chaves de idempotência por função do dono (sai o último `IgnoreQueryFilters`) | `IdempotencyStoreTests` (existentes) |
| 3.16 | `LexFlow.SecurityTests` com descoberta de endpoints: 401 anônimo, 403 sem permissão, 404 entre organizações, listas sem dados alheios | `EndpointSecurityTests` |

**ADR:** [0019 — Row-Level Security, papéis do banco e SecurityTests](adr/0019-rls-papeis-do-banco-e-securitytests.md).

**Critérios de aceite da Etapa 3 que fecham aqui:**

| Critério | Status | Evidência |
| --- | --- | --- |
| Usuário da Org A recebe 404 em qualquer recurso da Org B (teste automático cobre 100% dos endpoints) | ✅ | `EndpointSecurityTests.Resources_of_another_organization_or_user_answer_404` e `Every_route_with_an_id_has_a_cross_tenant_case` |
| SQL cru com `app.organization_id` da Org A não retorna linhas da Org B | ✅ | `RowLevelSecurityTests.Raw_sql_as_the_application_only_sees_the_organization_of_the_session` |

**Última verificação local (PR 3C):** `dotnet build` (0 warnings), `dotnet format --verify-no-changes`, 482 testes .NET em 13 projetos (Testcontainers; a API e os SecurityTests conectam como o login da aplicação) e 14 do AppHost, 23 Vitest, Playwright 4 contra o `ng serve` e 5 contra o AppHost (fluxo completo, com a API e o Worker no login da aplicação), `npm run lint`, sem pacotes vulneráveis. As migrations dos três módulos não têm mudança de modelo pendente.

**Pendências do PR 3C:**
1. Produção: provisionar o papel dono das migrations com `CREATEROLE` e `BYPASSRLS` e guardar no cofre a senha do login da aplicação (runbook do deploy).
2. O painel do Hangfire continua sem rota; a autorização dele entra quando houver painel.
3. Desempenho: um `set_config` por abertura de conexão; reavaliar no teste de carga.

### PR 3B — tenant real e permissões (concluído: PR #12 mergeado em 2026-10-17)

| # | Subtarefa | Testes |
| --- | --- | --- |
| 3.6 | Organização ativa (`X-Organization-Id`, claim, última escolhida ou única) validada contra a pertença; diretório `org.user_organizations` | `TenantAndPermissionTests` (Web.Common), `AccessAndPermissionsTests.Requests_for_an_organization_the_user_is_not_part_of_are_refused` |
| 3.7 | Política de permissão real (`PermissionRequirement`) e `PermissionService` (`IPermissionChecker`) com cache Redis de 5 min, invalidação imediata e por `RolePermissionsChangedIntegrationEvent` | `Member_without_the_permission_gets_403`, `Removing_a_permission_from_a_role_blocks_the_action_within_5_seconds_without_a_new_login` |
| 3.8 | `IResourceAuthorizer<T>` e decorators na ordem da seção 4.3; `ICurrentAccess` com os escopos do usuário | `PipelineTests.Resource_authorization_runs_after_validation_and_before_the_transaction`, `Denied_query_never_reaches_the_handler` |
| 3.9 | MFA obrigatório por organização (`PUT /api/v1/organization/security`), 403 `organization.mfa_required` | `Required_mfa_blocks_office_data_until_the_token_has_a_second_factor` |
| 3.10 | `GET /api/v1/me/access` e troca de organização (`POST /api/v1/me/active-organization`) | `Access_lists_organizations_and_permissions_and_the_user_can_switch_organization` |
| 3.11 | Membros e perfis: `GET /api/v1/organization/members`, `PUT .../members/{id}/roles`, `GET /api/v1/roles`, `PUT /api/v1/roles/{id}/permissions`, com as salvaguardas (administrador travado, último administrador, escopo da própria organização) | `Role_permissions_must_come_from_the_catalog_and_the_admin_role_is_locked`, `The_office_never_loses_its_last_admin_and_scopes_must_reference_its_structure` |

**ADR:** [0018 — Tenant real, permissões e MFA por organização](adr/0018-tenant-real-e-autorizacao.md). A ADR 0008 fica substituída.

**Critério de aceite da Etapa 3 que fecha aqui:** remover `settings.users.manage` de um perfil bloqueia a ação em até 5 s, sem novo login ✅ (`AccessAndPermissionsTests.Removing_a_permission_from_a_role_blocks_the_action_within_5_seconds_without_a_new_login`).

**Última verificação local (PR 3B):** `dotnet build` (0 warnings), `dotnet format --verify-no-changes`, 471 testes .NET em 12 projetos (Testcontainers, com Redis real) e 14 do AppHost, 23 Vitest, 4 Playwright (`ng serve`), `npm run lint`, `npm run build`, sem pacotes vulneráveis. Cobertura: Organization.Application 92,4% e Organization.Domain 88,3%. O OpenAPI só ganhou adições.

**CI do PR 3B:** o contrato OpenAPI falhou no Linux porque um comentário XML de várias linhas virava descrição com `
` no Windows e `
` no CI. O teste agora normaliza a quebra de linha dentro das strings e mostra a primeira linha diferente.

**Pendências do PR 3B:**
1. O consumidor `RolePermissionsChangedConsumer` (Worker) não tem teste próprio.
2. O SPA ainda não manda `X-Organization-Id` nem usa `GET /api/v1/me/access` (PR 3F); até lá vale a organização escolhida ou a única.

### PR 3A — módulo Organization, catálogo e onboarding (concluído: PR #11 mergeado em 2026-10-17)

| # | Subtarefa | Testes |
| --- | --- | --- |
| 3.1 | Módulo `Organization` (schema `org`): organização, membros, filiais, áreas, equipes, membros de equipe, centros de custo, sócios, perfis, permissões dos perfis e atribuições com escopo de dados | `DomainTests`, `OrganizationDatabaseTests` (migrations do zero e revertidas, unicidade dentro do tenant) |
| 3.2 | `PermissionCatalog` com todas as permissões previstas, agrupadas por módulo; permissões universais da própria conta | `PermissionCatalogTests`, `EndpointSecurityConventionTests.Every_required_permission_is_in_the_catalog` |
| 3.3 | Perfis padrão internos (seção 3.2) com matriz proposta (ADR 0017) | `PermissionCatalogTests.Default_roles_*` |
| 3.4 | Onboarding `POST /api/v1/organizations`: organização, perfis padrão e criador como Administrador do escritório; `TenantOverride` para os primeiros dados da organização nova | `OrganizationOnboardingTests`, `TenantOverrideTests` |
| 3.5 | Contrato OpenAPI e cliente TypeScript atualizados (só adições) | `OpenApiContractTests` |

**ADR:** [0017 — Organizações, catálogo de permissões e perfis padrão](adr/0017-organizacoes-catalogo-e-perfis-padrao.md) (Aceita em 2026-10-17: matriz dos perfis padrão aprovada).

**Última verificação local (PR 3A):** `dotnet build` (0 warnings), `dotnet format --verify-no-changes`, 454 testes .NET em 12 projetos (Testcontainers), 23 Vitest, 4 Playwright (`ng serve`), `npm run lint`, sem pacotes vulneráveis. Cobertura: Organization.Application 100% e Organization.Domain 84,8%. A migration `Initial` do Organization não tem mudança de modelo pendente.

**Fora da etapa (outras etapas da spec):** SSO, SCIM e restrição por IP (Etapa 16); planos e administrador da plataforma (Etapa 13); delegação temporária e alçadas (Etapa 8); feriados do escritório (Etapa 7).

---

## Etapa 2 — Identidade e autenticação (concluída em 2026-10-16)

**RFs:** RF-01 (cadastro, login, logout, recuperação de senha, MFA, bloqueio por tentativas, sessões). Ficam para depois: MFA obrigatório por organização, convites, troca de organização e restrição por IP/horário (Etapa 3); SSO, SCIM e passkeys (Etapa 16).

Plano aprovado em 2026-10-16, em 3 PRs: **A** (contas, senhas, e-mails), **B** (servidor OIDC, login, sessões, TOTP), **C** (frontend e e2e).

**Decisões de negócio confirmadas:**
- O cadastro é aberto (autoatendimento).
- MFA por TOTP e também por código enviado por e-mail.
- O refresh token é renovado a cada uso, vale 14 dias sem uso e no máximo 30 dias; depois disso é preciso entrar de novo.
- A verificação de senhas vazadas (Have I Been Pwned, k-anonymity) fica ligada por padrão, com chave para desligar.
- MFA é opcional por usuário nesta etapa: quem ativa escolhe TOTP ou código por e-mail como segundo fator (decisão de 2026-10-16). O MFA obrigatório por organização fica para a Etapa 3.
- Sem "lembrar este dispositivo" por enquanto: todo login de quem tem MFA pede o segundo fator.
- O SPA guarda os tokens em `sessionStorage` (decisão de 2026-10-16): sobrevive ao F5 na mesma aba e some ao fechar a aba; nada em `localStorage` (seção 5.3).

**Plano do PR C (proposto em 2026-10-16):** CORS só para a origem do SPA; Mailpit em porta fixa e rate limit configurável no AppHost; `core/auth` com `angular-auth-oidc-client` (PKCE, refresh rotativo, guard, interceptor); configuração em tempo de execução (`config.json`); página pública `/entrar`; feature `account` (perfil, sessões, ativação de TOTP com QR code e códigos de recuperação, ou código por e-mail) com SignalStore (`@ngrx/signals`); Vitest; e2e contra o AppHost do fluxo cadastro → confirmação (Mailpit) → login → TOTP → logout → login com TOTP.

**Plano do PR B (proposto em 2026-10-16), dividido em dois:**
- **B1:** OpenIddict (Authorization Code + PKCE, refresh rotativo com revogação em reuso), validação de token no lugar do esquema provisório (ADR 0008), páginas Razor de conta em pt-BR, sessões ligadas aos tokens (`sid`), bloqueio na 6ª senha errada, rate limit por IP e limpeza de tokens expirados.
- **B2:** MFA (TOTP com códigos de recuperação e código por e-mail), `GET /api/v1/me` e `GET|DELETE /api/v1/me/sessions`.

### PR C — frontend e e2e (concluído: PR #10 mergeado em 2026-10-16)

| # | Subtarefa | Testes |
| --- | --- | --- |
| 2.21 | CORS só para as origens do SPA; Mailpit em porta fixa e rate limit configurável no AppHost | `CorsTests`, `AppHostTests` |
| 2.22 | `core/auth`: `angular-auth-oidc-client` (PKCE, refresh rotativo com trava entre abas, `sessionStorage`), guards `canMatch`, `/entrar`, `/auth/callback`; configuração em tempo de execução (`config.json`) | `auth.spec.ts`, `load-app-config.spec.ts`, e2e `shell.spec.ts` |
| 2.23 | Shell com usuário, navegação e "Sair" (logout no servidor) | `shell.spec.ts` (Vitest) |
| 2.24 | "Minha conta": perfil, TOTP (QR code, chave, confirmação, códigos de recuperação), código por e-mail, sessões com "Encerrar"; SignalStore e erros traduzidos | `account-store.spec.ts`, `account-components.spec.ts` |
| 2.25 | E2E do critério de aceite contra o AppHost | `e2e/account-flow.spec.ts` |

**ADR:** [0016 — Autenticação no SPA e "minha conta"](adr/0016-autenticacao-no-spa.md).

**Critérios de aceite da Etapa 2:**

| Critério | Status | Evidência |
| --- | --- | --- |
| Fluxo e2e: cadastro → confirmação de e-mail (Mailpit) → login → ativação de TOTP → logout → login com TOTP | ✅ | `e2e/account-flow.spec.ts` (Playwright contra o AppHost; no CI com `E2E_MAILPIT_URL`) |
| Refresh token rotativo: reutilizar um antigo revoga a família inteira | ✅ | `OidcFlowTests.Reusing_an_old_refresh_token_revokes_the_whole_family` |
| 6ª tentativa de senha errada bloqueia a conta por 15 min | ✅ | `OidcFlowTests.Sixth_attempt_after_five_wrong_passwords_is_blocked_for_15_minutes` |

**Última verificação local (PR C):** `dotnet build` (0 warnings), `dotnet format --verify-no-changes`, 415 testes .NET em 10 projetos e 14 do AppHost, 23 Vitest, `npm run lint`, `npm run build`, `npm audit` e pacotes .NET sem vulnerabilidades. Playwright: 5 de 5 contra o AppHost (duas rodadas, incluindo o fluxo completo) e 4 contra o `ng serve` (o fluxo completo é pulado sem Mailpit).

**Pendências do PR C:**
1. CSP restritiva e demais cabeçalhos da seção 7.3 (HSTS, `Referrer-Policy`), que são a defesa principal dos tokens no `sessionStorage`.
2. Diretiva `lfCan` e permissões no `GET /api/v1/me` entram com o `PermissionCatalog` (Etapa 3).

### PR B2 — MFA e minha conta (concluído: PR #9 mergeado em 2026-10-16)

| # | Subtarefa | Testes |
| --- | --- | --- |
| 2.16 | TOTP em duas etapas (`POST /api/v1/me/mfa/totp` e `/confirm`), com 10 códigos de recuperação | `MfaAndProfileTests.Totp_is_enabled_in_two_steps_and_then_required_at_login`, `Confirming_without_starting_the_setup_is_refused` |
| 2.17 | Código por e-mail como segundo fator (`POST /api/v1/me/mfa/email`), enviado pelo Worker a cada login | `Email_code_is_sent_by_the_worker_at_each_login`, `AccountEmailConsumerTests.Sign_in_code_generated_by_the_worker_is_accepted_as_the_email_second_factor` |
| 2.18 | Página `/conta/segundo-fator` (TOTP, e-mail, recuperação); `amr` com `mfa` no token | `Wrong_second_factor_is_refused_and_a_recovery_code_works_only_once`, `SessionAndUserTests.Enabling_mfa_*` |
| 2.19 | `GET /api/v1/me`, `GET /api/v1/me/sessions` (com `current`, sem as sessões de refresh reutilizado) e `DELETE /api/v1/me/sessions/{id}` | `Profile_is_returned_for_the_signed_in_user_only`, `Open_sessions_are_listed_with_the_current_one_and_a_reused_refresh_hides_its_session`, `A_user_ends_their_own_session_but_not_someone_elses` |
| 2.20 | Contrato OpenAPI e cliente TypeScript atualizados (só adições) | `OpenApiContractTests` |

**Migration:** `MfaMethod` (coluna `identity.users.mfa_method`, padrão `None`).
**ADR:** [0015 — MFA e "minha conta"](adr/0015-mfa-e-minha-conta.md).

**Última verificação local (PR B2):** `dotnet build` (0 warnings), `dotnet format --verify-no-changes`, 412 testes .NET em 10 projetos (Testcontainers), 5 Vitest, 4 Playwright, `npm run lint`, sem pacotes vulneráveis. Cobertura: Identity.Application 94,3% e Identity.Domain 100%. A migration `MfaMethod` não tem mudança de modelo pendente.

**Pendências do PR B2:**
1. Desligar o MFA ou trocar o método exige reautenticação (*step-up*), que não está na especificação.
2. Etapa 3: conceder `identity.profile.read` e `identity.profile.manage` a todo usuário autenticado no `PermissionCatalog`.

### PR B1 — servidor OIDC, login e sessões (concluído: PR #8 mergeado em 2026-10-16)

| # | Subtarefa | Testes |
| --- | --- | --- |
| 2.8 | OpenIddict 7.7.2 (passthrough): `/connect/authorize`, `/connect/token`, `/connect/logout`; cliente `lexflow-web` público com PKCE, cadastrado pelo MigrationService (`IDatabaseSeeder`) | `OidcFlowTests.Login_with_pkce_issues_tokens_bound_to_a_new_session`, `OidcStoreTests.Web_client_is_public_requires_pkce_and_seeding_twice_keeps_a_single_client` |
| 2.9 | Validação local dos access tokens como esquema padrão (substitui a ADR 0008) | `OidcFlowTests.Access_token_authenticates_api_requests` (sem esquema de teste) |
| 2.10 | Refresh rotativo, tolerância zero de reuso; access token de 10 min | `OidcFlowTests.Reusing_an_old_refresh_token_revokes_the_whole_family` |
| 2.11 | Sessão ligada à autorização (`sid`): renovada a cada troca, recusada se revogada ou expirada; logout e redefinição de senha encerram a sessão e revogam os tokens | `Refresh_renews_the_session_and_is_refused_once_the_session_ends`, `Logout_ends_the_session_and_revokes_its_tokens`, `Password_reset_ends_every_open_session` |
| 2.12 | Páginas Razor pt-BR (`/conta/entrar`, `cadastro`, `confirmar-email`, `esqueci-a-senha`, `redefinir-senha`); cookie de login apagado ao emitir o código | `Account_pages_are_served_in_portuguese`, `Login_cookie_is_dropped_after_the_code_so_a_new_authorization_asks_for_the_password`, `AccountMessagesTests` |
| 2.13 | Bloqueio por tentativas; e-mail não confirmado não entra | `Sixth_attempt_after_five_wrong_passwords_is_blocked_for_15_minutes`, `Unconfirmed_email_cannot_sign_in` |
| 2.14 | Rate limit por IP (10/min) nas páginas de conta e em `/api/v1/auth/*` | `Public_account_endpoints_are_rate_limited_per_ip` |
| 2.15 | Limpeza diária de tokens no Worker | `OidcStoreTests.Cleanup_job_runs_against_the_real_tables` |

**Tabelas novas:** `identity.openiddict_applications`, `openiddict_authorizations`, `openiddict_scopes`, `openiddict_tokens` (migration `OpenIddict`).
**ADR:** [0014 — Servidor OIDC, login e sessões](adr/0014-servidor-oidc-login-e-sessoes.md).

**Critérios de aceite da etapa:**

| Critério | Status | Evidência |
| --- | --- | --- |
| Refresh token rotativo: reutilizar um antigo revoga a família inteira | ✅ | `OidcFlowTests.Reusing_an_old_refresh_token_revokes_the_whole_family` |
| 6ª tentativa de senha errada bloqueia a conta por 15 min | ✅ | `OidcFlowTests.Sixth_attempt_after_five_wrong_passwords_is_blocked_for_15_minutes` |
| Fluxo e2e cadastro → confirmação → login → TOTP → logout → login com TOTP | Pendente | depende do PR B2 (TOTP) e do PR C (telas e Playwright) |

**Última verificação local (PR B1):** `dotnet build` (0 warnings), `dotnet format --verify-no-changes`, 397 testes .NET em 10 projetos (Testcontainers), 5 Vitest, 4 Playwright, `npm run lint`, sem pacotes vulneráveis, contrato OpenAPI sem mudança. Cobertura: Identity.Application 96,5% e Identity.Domain 100%. A migration `OpenIddict` não tem mudança de modelo pendente.

**Pendências do PR B1:**
1. `ForwardedHeaders` no deploy (IP real do rate limit e HTTPS atrás de proxy).
2. ~~PR B2: sessão de refresh reutilizado; MFA; `/api/v1/me`~~: feitos no PR B2 (a sessão some da lista e não renova).
3. Client Credentials na Etapa 14.

### PR A — contas, senhas e e-mails (concluído: PR #7 mergeado em 2026-10-16, CI verde)

| # | Subtarefa | Testes |
| --- | --- | --- |
| 2.1 | Convenções de módulo em `ModuleModel`/`IModuleDbContext`, usadas também por contexto que não herda de `ModuleDbContextBase`; marcações de auditoria por configuração (`IsSensitive`, `IsNotAudited`) | `PersistenceTests`, `AuditTrailTests` (existentes); `AccountEmailConsumerTests.Audit_trail_records_the_user_without_secrets_and_never_the_keys` |
| 2.2 | `PlatformIntegrationEvent`: evento sem organização, consumido sem tenant | `OutboxTests.Platform_event_is_published_without_tenant_and_consumed_without_tenant`, `Platform_event_with_organization_is_rejected` |
| 2.3 | Módulo Identity: `ApplicationUser`, `Session` (14 dias sem uso, máximo 30), Identity + EF Core no schema `identity`, migration `Initial`, passkeys fora do modelo (Etapa 16) | `SessionAndUserTests`, `MigrationTests` (Identity) |
| 2.4 | Política de senha: mínimo 12, máximo 128, senhas vazadas (Pwned Passwords, falha aberta, desligável); bloqueio 5 tentativas / 15 min configurado (prova no PR B, com o login) | `PwnedPasswordValidatorTests`, `AccountRulesTests`, `AuthEndpointsTests.Short_password_is_rejected_with_its_code_even_for_an_existing_email` |
| 2.5 | `POST /api/v1/auth/register`, `confirm-email`, `forgot-password`, `reset-password`, públicos com motivo (`AllowPublicAccess`) e sem revelar se o e-mail tem conta | `AuthEndpointsTests` (11 casos), `EndpointSecurityConventionTests`, `SecurityAndCorrelationTests.Public_endpoint_records_its_reason_and_skips_authentication` |
| 2.6 | E-mails de conta em pt-BR (Scriban), enviados pelo Worker a partir do outbox; token gerado no Worker; anel de chaves do Data Protection no banco | `AccountEmailComposerTests`, `AccountEmailConsumerTests`, `AuthEndpointsTests.Link_sent_by_the_worker_confirms_the_email`, `Forgot_password_resets_with_a_single_use_link` |
| 2.7 | Contrato OpenAPI e cliente TypeScript atualizados (só adições) | `OpenApiContractTests` |

**Tabelas:** `identity.users`, `user_claims`, `user_logins`, `user_tokens`, `sessions`, `data_protection_keys`, `processed_integration_events`.
**Telas:** nenhuma (páginas Razor no PR B, SPA no PR C). Os links dos e-mails apontam para `/conta/confirmar-email`, `/conta/redefinir-senha` e `/conta/esqueci-a-senha` no servidor de identidade (`Identity:PublicUrl`), que o PR B cria.
**ADR:** [0013 — Contas, senhas e e-mails de conta](adr/0013-contas-senhas-e-emails.md) (Aceita em 2026-10-16: cadastro que não revela contas e eventos da plataforma aprovados; seção 4.5 da spec atualizada).

**Critérios de aceite da etapa:** nenhum fecha no PR A. O fluxo e2e e o refresh rotativo dependem do OpenIddict (PR B) e das telas (PR C). O bloqueio na 6ª tentativa está configurado, mas só é provado com o login.

**Última verificação local (PR A):** `dotnet build` (0 warnings), `dotnet format --verify-no-changes`, 382 testes .NET em 10 projetos (Docker local, Testcontainers), 5 Vitest e 4 Playwright, `npm run lint`, sem pacotes vulneráveis. Cobertura: Identity.Application 98,2% e Identity.Domain 95,3%; as demais camadas seguem acima de 90%. A migration `Initial` do Identity não tem mudança de modelo pendente.

**Pendências do PR A:**
1. Anel de chaves do Data Protection sem criptografia em repouso: proteger com `IKeyProvider`/cofre antes de produção (ADR 0013).
2. PR B: rate limit por IP nos endpoints públicos de conta junto com o de login; revogar sessões ao redefinir a senha; páginas Razor nos caminhos `/conta/*` dos links.
3. Spec, seção 7.1: a tabela é `identity.users` (snake_case, seção 6.1), e não `identity.Users`.

---

## Etapa 1 — Building blocks (2026-10-16)

**RFs:** base para todos. Feriados e tribunais preparam o RF-03 e o RF-09; a auditoria prepara o RF-45.

Plano aprovado em 2026-10-16, em 3 PRs empilhados e mergeados: **A** #4 (SharedKernel e value objects), **B** #5 (Application.Abstractions e Web.Common) e **C** #6 (infraestrutura, Platform e mensageria).

**Decisões de negócio confirmadas:**
- O seed de feriados tem só os feriados nacionais de lei federal. Forenses, estaduais, municipais, Carnaval, Corpus Christi e recesso ficam para a Etapa 7.
- Os números CNJ de teste são gerados com dígito verificador correto, sem processos reais.

### Entregue

| # | Subtarefa | Testes |
| --- | --- | --- |
| 1.1 | SharedKernel: `Entity`, `AggregateRoot`, `ValueObject`, `Result`/`Error`, `ITenantOwned`, `ICaseScoped`, `ISoftDeletable`, `IAuditable`, `[NotAudited]`, `[Sensitive]` | `ResultTests`, `EntityTests` |
| 1.2 | Value objects: `Cpf`, `Cnpj` (inclui alfanumérico), `CnjNumber`, `OabNumber`, `Money`, `Email`, `Phone`, `Cep` | `CnjNumberTests`, `DocumentTests`, `ContactTests`, `MoneyTests` |
| 1.3 | Regras do CLAUDE.md no build e no IL (ADR 0006) | `BannedApiTests.*` |
| 1.4 | Pipeline: `ICommand`/`IQuery`, decorators Logging → Validação → Transação, `IUnitOfWork` por módulo, `IClock`, `IIdGenerator`, Scrutor (ADR 0007) | `PipelineTests`, `ClockTests`, `HandlerConventionTests` |
| 1.5 | Web.Common: ProblemDetails, `X-Correlation-Id`, `RequirePermission` e autenticação provisórios (ADR 0008), cursor, `IModule` | `ProblemDetailsTests`, `SecurityAndCorrelationTests`, `CursorPaginationTests` |
| 1.6 | EF Core base: `ModuleDbContextBase` (filtros nomeados de tenant e soft delete, `xmin`), interceptors de eventos de domínio, tenant, colunas de controle e auditoria | `PersistenceTests`, `AuditTrailTests` |
| 1.7 | Módulo Platform: `Job`, `IdempotencyKey`, `Holiday` (regras), `Court` (107); migrations; trilha encadeada e append-only (ADR 0010); `MigrationService` (ADR 0011) | `HolidayCalendarTests`, `JobAndCourtTests`, `MigrationTests`, `AuditLogTests` |
| 1.8 | Outbox/inbox Wolverine (PostgreSQL + RabbitMQ), consumo idempotente por `EventId` (ADR 0009) | `OutboxTests` |
| 1.9 | Adaptadores: S3, SMTP, ClamAV (cliente próprio), Hangfire com tenant do agendador | `AdaptersTests` |
| 1.10 | `GET /api/v1/jobs/{id}`, `Idempotency-Key` com armazenamento e limpeza diária | `JobsEndpointTests`, `IdempotencyFilterTests`, `IdempotencyStoreTests` |
| 1.11 | Contrato versionado (`docs/openapi/v1.json`), `oasdiff` no CI, cliente TypeScript gerado, cobertura ≥ 80% (ADR 0012) | `OpenApiContractTests`, `scripts/coverage_gate.py` |

**Tabelas:** `audit.audit_logs`, `platform.jobs`, `platform.idempotency_keys`, `platform.holidays`, `platform.courts`, `platform.processed_integration_events`, além dos schemas `wolverine` e `hangfire`, mantidos pelas próprias bibliotecas.
**Endpoint:** `GET /api/v1/jobs/{id}` (permissão `platform.jobs.read`).
**Telas:** nenhuma; existe só o cliente gerado em `src/app/api`.

### Critérios de aceite e evidências

| Critério | Status | Evidência |
| --- | --- | --- |
| Testes unitários dos value objects com casos válidos e inválidos (≥ 20 CNJ válidos e ≥ 20 inválidos) | ✅ | `CnjNumberTests.Valid_numbers_are_accepted` (22 números, segmentos J = 1 a 9), `Valid_numbers_pass_independent_mod97_check` (`BigInteger`), `Invalid_numbers_are_rejected` (20) e `Null_is_rejected`; `DocumentTests`, `ContactTests` e `MoneyTests` para os demais value objects |
| Evento de integração gravado no outbox é publicado e consumido uma única vez, mesmo com reentrega | ✅ | `OutboxTests.Event_saved_in_outbox_is_published_and_consumed_exactly_once`, `Redelivered_event_is_not_processed_twice`, `Rolled_back_transaction_publishes_nothing` e `Consumer_runs_with_the_tenant_of_the_event` (Wolverine, PostgreSQL e RabbitMQ reais) |
| `SaveChanges` gera linha em `audit.audit_logs` com antes e depois | ✅ | `AuditTrailTests.Save_changes_writes_audit_rows_with_before_and_after` e `Sensitive_and_not_audited_fields_never_enter_changes`; com a migration real, `AuditLogTests.Save_changes_writes_chained_audit_row_with_before_and_after`, além de cadeia sob concorrência, append-only e adulteração detectada |

**Última verificação local:** `dotnet build` (0 warnings), `dotnet format --verify-no-changes`, 333 testes .NET (8 projetos), 5 Vitest e 4 Playwright passando, cobertura entre 90,3% e 98,3% nas camadas do portão, `npm run lint`, `npm test`, `npm run build` e cliente da API determinístico.

### ADRs

| ADR | Status |
| --- | --- |
| [0006 — Regras do CLAUDE.md no compilador e no IL](adr/0006-regras-no-compilador.md) | Aceita |
| [0007 — Pipeline de casos de uso próprio](adr/0007-pipeline-de-casos-de-uso.md) | Aceita |
| [0008 — Autenticação, permissão e tenant provisórios](adr/0008-autenticacao-e-tenant-provisorios.md) | Aceita |
| [0009 — Mensageria, jobs e adaptadores](adr/0009-mensageria-jobs-e-adaptadores.md) | Aceita |
| [0010 — Trilha de auditoria](adr/0010-trilha-de-auditoria.md) | Aceita |
| [0011 — Dados de referência e MigrationService](adr/0011-dados-de-referencia-e-migrations.md) | Aceita |
| [0012 — Contrato da API e cobertura](adr/0012-contrato-da-api-e-cobertura.md) | Aceita |

### Pendências

1. **Etapa 2:** substituir o esquema de autenticação provisório pelo OpenIddict.
2. **Etapa 3:**
   - validar `org_id` contra `OrganizationMember` e aceitar o cabeçalho `X-Organization-Id`;
   - ~~RLS e papéis do banco, incluindo o `REVOKE UPDATE, DELETE` em `audit.audit_logs`~~: feito no PR 3C;
   - permissão `platform.jobs.read` no `PermissionCatalog`;
   - decorator de autorização de recurso;
   - painel do Hangfire com autorização (sem rota exposta até hoje; ver ADR 0019);
   - ~~`SecurityTests` cobrindo `GET /api/v1/jobs/{id}`~~: coberto no PR 3C.
3. **Etapa 7:** feriados forenses, estaduais e municipais, recesso e feriados do escritório.
4. **Desempenho:** pré-gerar o código do Wolverine (`TypeLoadMode.Static`) para a inicialização em produção.
5. **Etapa 9:** `StreamMaxLength` do ClamAV alinhado ao limite de upload.
6. **Testes:** a inbox do Wolverine (descarte por envelope) não tem teste próprio. O efeito único é garantido e testado pela camada de `processed_integration_events`.
7. Continuam da Etapa 0: Worker sem endpoint de saúde; política do npm `allow-scripts`; licença do Redis 8.

---

## Etapa 0 — Bootstrap do repositório (2026-10-16)

**RFs:** nenhum (infraestrutura).

### Entregue (um commit por subtarefa)

| # | Subtarefa | Principais artefatos |
| --- | --- | --- |
| 0.1 | Base do repositório | `global.json` (SDK 10.0.302 + runner MTP), `Directory.Build.props` (net10.0, Nullable, TreatWarningsAsErrors, `latest-recommended`), `Directory.Packages.props` (CPM), `.editorconfig`, Meziantou.Analyzer; `docs/specs` → `docs/spec` |
| 0.2 | Solução e esqueleto | `LexFlow.sln`, BuildingBlocks (`SharedKernel`, `Application.Abstractions`, `Infrastructure.Common`, `Web.Common`), `LexFlow.Api`, `LexFlow.Worker`, `infra/`, `deploy/` |
| 0.3 | ServiceDefaults + API | Serilog (mantém o provedor OTel), OpenTelemetry, `GET /health/live`, `GET /health/ready` (PostgreSQL, Redis, RabbitMQ, armazenamento S3), OpenAPI + Scalar em Development; `tests/LexFlow.Api.IntegrationTests` (Testcontainers) |
| 0.4 | Testes de arquitetura | `tests/LexFlow.ArchitectureTests`: regras da seção 4.2 por namespace, BuildingBlocks, auto-testes com fixture de violações |
| 0.5 | Frontend | `src/Web/lexflow-web`: Angular 22 zoneless, standalone/OnPush (exigido no lint), Material 3 com tokens CSS e tema claro/escuro, Transloco pt-BR (en preparado, sem HTTP), ESLint + Prettier, Vitest, Playwright + axe; shell vazio em pt-BR |
| 0.6 | AppHost | PostgreSQL (pgvector pg17), Redis, RabbitMQ, RustFS (S3), Gotenberg, ClamAV, Mailpit, API, Worker, Web; segredo do armazenamento gerado pelo Aspire; `tests/LexFlow.AppHost.Tests` |
| 0.7 | CI | `.github/workflows/ci.yml`: build, format, pacotes vulneráveis, arquitetura, integração, frontend (audit, lint, Vitest, build), AppHost + Playwright contra o AppHost, CodeQL, Gitleaks, Trivy (fs) |
| 0.8 | Documentação | ADRs 0001–0005 e este arquivo |

Não há entidades, tabelas, migrations nem endpoints de negócio nesta etapa.

### Critérios de aceite e evidências

| Critério | Status | Evidência |
| --- | --- | --- |
| `dotnet run --project src/Aspire/LexFlow.AppHost` sobe API, Worker, Web e todos os containers, todos saudáveis no dashboard | ✅ | `AppHostTests.Resource_becomes_healthy` (postgres, lexflow, redis, rabbit, storage, gotenberg, clamav via `zPING`/`PONG`, mailpit, api, web) e `AppHostTests.Resource_is_running` (worker, que não tem endpoint HTTP de saúde). Verificação manual: `dotnet run --launch-profile http` subiu tudo e o dashboard ficou em `http://localhost:15211` |
| `GET /health/ready` retorna 200 com PostgreSQL, Redis, RabbitMQ e MinIO verificados | ✅ (MinIO → RustFS, ADR 0004) | `HealthEndpointsTests.Ready_returns_200_when_all_dependencies_are_up`; `HealthEndpointsTests.Ready_returns_503_when_a_dependency_is_down` (4 casos, um por dependência) prova que cada uma é verificada; `AppHostTests.Api_ready_endpoint_returns_200` |
| Frontend abre com layout vazio em pt-BR | ✅ | e2e `shell.spec.ts › opens the empty layout in pt-BR`, `skip link moves focus to the main content`, `has no WCAG 2.1 AA accessibility violations` (claro e escuro); Vitest `Shell`, `DashboardPage`, `translations`; `AppHostTests.Web_serves_the_angular_app_in_pt_BR` |
| CI verde no primeiro PR; um teste de arquitetura propositalmente quebrado faz o CI falhar | ✅ | PR #3: CI verde no commit `e1cc7c7` (run 38948667829; Backend, Frontend, AppHost + E2E, Gitleaks e Trivy passaram, CodeQL pulado, ver pendência 10). Commit temporário `8a2c0f5` (`LexFlow.Demo.Domain` usando `LexFlow.Demo.Infrastructure`): run 38949537138 falhou em `ModuleDependencyTests.Module_layers_respect_dependency_rules` com "Demo.Domain viola a seção 4.2: LexFlow.Demo.Domain.DemoEntity". Revertido em seguida. As regras também são provadas por `LayerRulesSelfTests.*` |

**Última verificação local completa:** `dotnet build` (0 warnings), `dotnet format --verify-no-changes`, `dotnet test --solution LexFlow.sln` (33/33), `npm run lint`, `npm test` (5/5), `npx playwright test` (4/4, contra o `ng serve` e contra o AppHost), `dotnet list package --vulnerable` e `npm audit` sem achados.

### ADRs criadas

| ADR | Status |
| --- | --- |
| [0001 — Monólito modular](adr/0001-monolito-modular.md) | Aceita |
| [0002 — Stack e licenças dos pacotes](adr/0002-stack.md) | Aceita |
| [0003 — Módulos criados por etapa](adr/0003-modulos-criados-por-etapa.md) | Aceita |
| [0004 — RustFS no lugar do MinIO](adr/0004-armazenamento-s3-local-rustfs.md) | Aceita (spec atualizada: seções 2.3, 4.1, 8.1, 8.2, 8.3, 9 e 13.1) |
| [0005 — SonarAnalyzer fora até revisão da licença](adr/0005-sonaranalyzer-fora.md) | Aceita |

### Pendências

1. **Revisão e merge do PR #3** (https://github.com/iagogoliveira/ProjetoLaw/pull/3).
2. ~~Revisão humana das ADRs 0003, 0004 e 0005~~: aceitas em 2026-10-16; spec atualizada (MinIO → RustFS).
3. **Checagem de breaking changes do OpenAPI (`oasdiff`)** e **cliente TypeScript gerado** (`@hey-api/openapi-ts`): entram quando houver o primeiro endpoint de negócio, porque ainda não há baseline.
4. **Trivy em imagens:** hoje varre só o filesystem. A varredura de imagens entra com os Dockerfiles do `deploy-staging.yml`.
5. **Cobertura ≥ 80% em Domain/Application:** ainda não há código nessas camadas. Ligar a coleta de cobertura (extensão do Microsoft.Testing.Platform) na Etapa 1.
6. **Worker sem endpoint de saúde:** registra os health checks das dependências, mas não os expõe. A observabilidade do Worker (Wolverine e Hangfire) entra na Etapa 1.
7. **`/health/*` sem autenticação em todos os ambientes:** responde só o status agregado, sem detalhes. Revisar junto com as proteções da Etapa 3 e da seção 7.3.
8. **npm 11 `allow-scripts`:** aparecem avisos sobre scripts de instalação não aprovados (`esbuild`, `lmdb`, `@parcel/watcher`, `msgpackr-extract`). O build funciona sem eles; decidir a política de aprovação (`npm approve-scripts`).
9. **Licença do Redis 8** (RSALv2/SSPLv1/AGPLv3): aceitável para uso interno. Avaliar Valkey antes de qualquer distribuição (ADR 0002).
10. ~~CodeQL desligado~~: o repositório ficou público em 2026-10-16 e o CodeQL voltou a rodar sem condição. Se o repositório voltar a ser privado, será preciso o GitHub Code Security.
11. **CI no GitHub:** o evento `pull_request` não disparou para o commit `e1cc7c7`, e o CI rodou por `workflow_dispatch`. Nos commits seguintes o evento voltou a disparar normalmente.
12. **Ajustes de CI descobertos no primeiro PR:** o runner Linux não confia no certificado de dev, então o readiness usa o endpoint `http`; o `dotnet run` do AppHost com `AspireUseCliBundle` só funciona em Debug; CodeQL precisa de `actions: read` e Gitleaks de `pull-requests: read`.
13. **PRs #1 e #2 (fechados sem merge, revisados em 2026-10-16):** eram uma tentativa anterior da Etapa 0 com SQL Server 2025, Azurite e OpenSearch, descartada quando a stack passou a ser PostgreSQL. Foi aproveitado o health check do ClamAV (`ClamAvHealthCheck`, protocolo clamd). Ficam como referência para a Etapa 1: a geração do `docs/openapi/v1.json` no build com o job `oasdiff` (pendência 3), o `proxy.conf.js` do Angular para `/api` e o writer JSON do `/health/ready` (só status e duração por dependência).
