# ADR 0018 — Tenant real, permissões e MFA por organização

- **Status:** Aceita
- **Data:** 2026-10-17
- **Etapa:** 3 (PR 3B)

## Contexto

A seção 6.2 pede o `ITenantContext` resolvido por requisição e validado contra `OrganizationMember`. A seção 7.1 pede permissões fora do token, resolvidas no servidor e cacheadas por 5 min com invalidação por evento. A seção 7.2 pede a autorização em duas etapas (endpoint e recurso), e o RF-01, o MFA obrigatório por organização (decisão de 2026-10-17). A ADR 0008 deixou isso provisório.

## Decisão

1. **Organização ativa:**
   - vem do cabeçalho `X-Organization-Id` ou da claim `org_id`; sem nenhum dos dois, vale a última escolhida (`POST /api/v1/me/active-organization`) ou a única;
   - é **sempre validada contra a pertença**, e pedir uma organização da qual o usuário não é membro dá 403 `organization.not_a_member`;
   - sem o serviço de pertença registrado, não há tenant: o que vem do cliente nunca é aceito sem checagem.
   - Os tokens **não** levam `org_ids` nem `role_ids`. A pertença e as permissões mudam a qualquer momento e são resolvidas no servidor, então não ficam desatualizadas no token.
2. **Diretório `org.user_organizations`** (usuário → organização, com a última escolha): é dado da plataforma, sem tenant e fora da auditoria, mantido na mesma transação que o membro. Saber de quais escritórios alguém é membro é uma consulta entre tenants, e `IgnoreQueryFilters` é proibido fora do Platform. A migration preenche o diretório com os membros que já existem.
3. **Permissões:**
   - o `PermissionPolicyProvider` gera `PermissionRequirement`, e o `PermissionAuthorizationHandler` consulta o `IPermissionChecker` do módulo Organization;
   - as permissões universais bastam com login; as demais exigem organização ativa, pertença e perfil;
   - o resultado guarda as permissões com os escopos de cada uma (`AccessSnapshot`).
4. **Cache no Redis, 5 min** (`StackExchange.Redis` 2.13.17, MIT):
   - as permissões ficam sob uma versão por organização, e invalidar é incrementar a versão (descarta as de todos os membros de uma vez);
   - toda mudança de acesso (permissões do perfil, atribuições, política de MFA) invalida logo após gravar e de novo ao consumir `RolePermissionsChangedIntegrationEvent` no Worker, depois do commit. A segunda vez cobre leituras concorrentes que guardaram o estado anterior no intervalo;
   - sem Redis, as permissões são lidas do banco: mais lento, nunca menos correto.
5. **MFA obrigatório** (`PUT /api/v1/organization/security`):
   - com a política ligada e um token sem `amr=mfa`, as permissões não universais respondem 403 `organization.mfa_required`;
   - as universais continuam livres, para o usuário conseguir ativar o segundo fator em "Minha conta".
6. **Autorização de recurso:** `IResourceAuthorizer<TRequest>` com decorators de comando e consulta, na ordem da seção 4.3 (Logging → Validação → Autorização de recurso → Transação). Os módulos leem os escopos do usuário por `ICurrentAccess`. O primeiro uso real será o de casos (Etapa 5).
7. **Salvaguardas:**
   - o perfil Administrador do escritório tem sempre todas as permissões (`organization.admin_role_locked`);
   - o escritório nunca fica sem administrador (`organization.last_admin`);
   - o escopo de equipe, área ou filial precisa referenciar uma entidade da própria organização.
8. **Enums da API como texto em camelCase** (`"team"`, `"practiceArea"`): o primeiro enum exposto foi o escopo de dados.
9. **Endpoints novos:**
   - `GET /api/v1/me/access`, que responde organizações, a ativa, as permissões e se a organização exige MFA. É separado do `GET /api/v1/me` do Identity, porque os módulos não se acessam;
   - `POST /api/v1/me/active-organization`;
   - `GET /api/v1/organization/members` e `PUT /api/v1/organization/members/{id}/roles`;
   - `GET /api/v1/roles` e `PUT /api/v1/roles/{id}/permissions`;
   - `PUT /api/v1/organization/security`.

## Consequências

- O critério de aceite "remover `settings.users.manage` bloqueia em até 5 s sem novo login" é provado em `AccessAndPermissionsTests`.
- Os perfis e o escopo do usuário ainda não filtram listas: isso fica com cada módulo de negócio, nos autorizadores de recurso e nas consultas.
- O consumidor do evento de invalidação não tem teste próprio. O efeito imediato, sem o Worker, é o que os testes provam.
- RLS, papéis do banco e os SecurityTests automáticos entram no PR 3C.
