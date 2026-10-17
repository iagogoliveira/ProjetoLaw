# ADR 0008 — Autenticação, permissão e tenant provisórios até as Etapas 2 e 3

- **Status:** Aceita; a parte de autenticação foi substituída pela [ADR 0014](0014-servidor-oidc-login-e-sessoes.md) (OpenIddict) na Etapa 2
- **Data:** 2026-10-16
- **Etapa:** 1

## Contexto

A Etapa 1 cria o primeiro endpoint de dados de escritório (`GET /api/v1/jobs/{id}`) e o `RequirePermission`, que a spec chama de "placeholder até Etapa 3". O login (OpenIddict) chega na Etapa 2. O `ITenantContext` real, que valida a pertença do usuário à organização, chega na Etapa 3, junto com o RLS.

## Decisão

- **Autenticação:** o esquema padrão é o `UnconfiguredAuthenticationHandler`, que nunca autentica. Assim, todo endpoint com permissão responde **401 com ProblemDetails** (`auth.unauthenticated`) até a Etapa 2 registrar o OpenIddict. Os testes substituem esse esquema por um de teste.
- **Permissão:** `RequirePermission("modulo.recurso.acao")` faz duas coisas:
  - grava `RequiredPermissionAttribute` nos metadados do endpoint, para que os SecurityTests da Etapa 3 e a tela de perfis possam ler;
  - exige a política `permission:{nome}`. O `PermissionPolicyProvider` atende qualquer política `permission:*` e, por enquanto, exige só usuário autenticado. Na Etapa 3 ele passa a consultar o `PermissionService`.
- **Usuário e tenant:** `ICurrentUser` lê a claim `sub` e `ITenantContext` lê a claim `org_id`, sem validar pertença. Endpoint de escritório sem `org_id` falha ao pedir `RequiredOrganizationId`.

## Consequências

- Nenhum endpoint de dados fica aberto por engano nesse período: sem autenticação, a resposta é sempre 401.
- A troca nas Etapas 2 e 3 não mexe nos endpoints: muda só o esquema de autenticação, o provedor de políticas e a implementação de `ITenantContext`.
- **Pendência da Etapa 3:** validar `org_id` contra `OrganizationMember`, aceitar o cabeçalho `X-Organization-Id` e aplicar o RLS.
