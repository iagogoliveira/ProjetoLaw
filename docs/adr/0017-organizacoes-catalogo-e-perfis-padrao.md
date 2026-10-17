# ADR 0017 — Organizações, catálogo de permissões e perfis padrão

- **Status:** Aceita em 2026-10-17 (matriz dos perfis padrão, item 4, aprovada pelo responsável)
- **Data:** 2026-10-17
- **Etapa:** 3 (PR 3A)

## Contexto

A Etapa 3 pede o módulo `Organization`, um `PermissionCatalog` com todas as permissões previstas (inclusive de módulos futuros) e um onboarding que cria os perfis padrão da seção 3.2. A especificação descreve cada perfil em uma frase, mas não lista as permissões de cada um. Decisão de 2026-10-17: no onboarding entram só os perfis internos.

## Decisão

1. **Módulo `Organization`, schema `org`:** `organizations`, `members`, `roles`, `role_permissions`, `member_role_assignments`, `branches`, `practice_areas`, `teams`, `team_members`, `cost_centers` e `partners`.
   - Todo dado de escritório é `ITenantOwned`, e todo índice começa por `organization_id`.
   - A unicidade vale dentro do tenant, com índice parcial `NOT is_deleted`: membro por usuário, nome de perfil, nome de filial, área e equipe, código de centro de custo e um perfil padrão de cada tipo.
   - `Organization` não é `ITenantOwned`: o `Id` dela é o próprio `OrganizationId`. A RLS da tabela (PR 3C) usa `id`.
   - `OrganizationMember.UserId` aponta para `identity.users` sem chave estrangeira, porque os módulos não se referenciam no banco.
2. **Catálogo** em `Organization.Contracts`, para outros módulos e a autorização (PR 3B) lerem.
   - Nomes `modulo.recurso.acao` ou `modulo.acao`, com descrição em pt-BR e grupo da tela de perfis.
   - **Permissões universais**, concedidas a todo usuário autenticado e fora da matriz: `identity.profile.read`, `identity.profile.manage` e `organizations.create`.
   - Um teste falha se algum endpoint exigir uma permissão que não está no catálogo.
3. **Onboarding:** `POST /api/v1/organizations`, aberto a qualquer usuário autenticado. Cria a organização, os 6 perfis padrão e o membro do criador com o perfil Administrador do escritório e escopo organização.
   - Os primeiros dados da organização recém-criada são gravados com um `TenantOverride` de escopo, só para esse caso.
   - Tudo fica na trilha de auditoria, sob a organização nova.
4. **Matriz dos perfis padrão (aprovada em 2026-10-17, editável por escritório):**

| Perfil | Permissões | Escopo sugerido |
| --- | --- | --- |
| Administrador do escritório | Todas as atribuíveis (configurações e todo o conteúdo) | Organização |
| Sócio | Todo o conteúdo, aprovações e BI; das configurações, só `settings.users.read` e `settings.audit.read` | Organização |
| Advogado | Pessoas (ver, cadastrar, editar), conflito, oportunidades e propostas (sem aprovar), casos (ver, cadastrar, editar, exportar), publicações (ver), prazos, agenda e tarefas, documentos (sem excluir), contratos de honorários (ver), timesheet próprio, despesas (lançar), IA (pesquisa e minutas), relatórios (ver) | Equipe |
| Estagiário/paralegal | Pessoas e casos (ver, cadastrar, editar), prazos e agenda (ver), tarefas, documentos (ver e enviar), timesheet próprio, pesquisa de IA. Nenhuma aprovação nem exclusão | Próprio |
| Controladoria jurídica | Casos (ver), publicações (ver, triar, distribuir), prazos (cadastrar, cumprir, conferir), agenda e tarefas, workflow (ver), documentos (ver), relatórios (ver) | Organização |
| Financeiro | Todo o financeiro (inclui aprovar faturas e despesas), pessoas (ver, para faturar), relatórios (ver e exportar). Nenhum conteúdo jurídico (casos, documentos, publicações) | Organização |

   Todos os perfis menos o Administrador recebem também `platform.jobs.read`, para acompanhar as próprias operações longas. O escopo sugerido só pré-preenche a atribuição de perfil (PR 3D); quem decide é o administrador.

## Consequências

- O `RequirePermission` ainda exige só login. A checagem real (perfil + escopo, cache de 5 min) chega no PR 3B, junto com a validação de pertença do tenant.
- Renomear uma permissão publicada exige migration dos perfis gravados.
- Os perfis Cliente do portal e Correspondente entram na Etapa 12; o Administrador da plataforma, na Etapa 13.
