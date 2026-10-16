# ADR 0013 — Contas, senhas e e-mails de conta

- **Status:** Aceita em 2026-10-16 (itens 2 e 3 aprovados pelo responsável; seção 4.5 da spec atualizada)
- **Data:** 2026-10-16
- **Etapa:** 2 (PR A)

## Contexto

A Etapa 2 pede cadastro, confirmação de e-mail, recuperação de senha, política de 12 caracteres com verificação de senhas vazadas (seção 7.1) e e-mails transacionais em pt-BR enviados pelo Worker (tarefa 5). Usuários são globais: uma pessoa pode pertencer a vários escritórios (RF-02), então os dados de conta não têm `OrganizationId`. O DbContext do Identity herda de `IdentityUserContext`, não de `ModuleDbContextBase`.

## Decisão

1. **Convenções de módulo sem herança.** As regras do `ModuleDbContextBase` (schema, filtros, `xmin`, auditoria, outbox) foram para `ModuleModel.Apply` e a interface `IModuleDbContext`. O contexto do Identity aplica as mesmas convenções. Propriedades de tipos que não podemos anotar (`PasswordHash`, `SecurityStamp`, `ConcurrencyStamp`, tokens do usuário) são marcadas como sensíveis ou não auditadas por configuração do EF (`IsSensitive()`, `IsNotAudited()`).
2. **Eventos de integração da plataforma.** `PlatformIntegrationEvent` é publicado sem organização (`OrganizationId` vazio) e consumido sem tenant, então dados de escritório ficam inacessíveis no consumidor. Publicar um desses eventos com organização é erro de programação. A seção 4.5 continua valendo para os eventos de escritório.
3. **Cadastro e "esqueci a senha" não revelam contas.** Os dois respondem 202 em qualquer pedido válido. Se o e-mail já tem conta, o dono recebe o aviso "você já tem uma conta" com o link de recuperação. A senha é validada antes de consultar o e-mail, então o resultado não depende da existência da conta. Confirmação e redefinição dão o mesmo `identity.invalid_token` para usuário inexistente e para token inválido. Recomendação OWASP; a alternativa mais simples seria responder 409 no cadastro.
4. **E-mails pelo Worker, sem segredo na fila.** A API publica `AccountEmailRequestedIntegrationEvent(UserId, Kind)` pelo outbox, no mesmo commit da conta. O Worker gera o token na hora do envio, monta o link (`Identity:PublicUrl` + `/conta/...`, páginas do PR B) e envia. O envio é "pelo menos uma vez": numa reentrega depois de envio sem registro, o e-mail sai de novo com outro token, também válido.
5. **Anel de chaves do Data Protection no banco** (`identity.data_protection_keys`), com o nome de aplicação `LexFlow`, para a API aceitar os tokens gerados pelo Worker. O acesso é por um `DataProtectionKeysDbContext` próprio, sem interceptors, porque o repositório do Data Protection grava com `SaveChanges` síncrono e chaves não entram na auditoria nem no outbox. A tabela é criada pela migration do Identity. Os links valem 24 h; o de redefinição é de uso único, porque a troca de senha renova o security stamp.
6. **Senhas vazadas** pela API Pwned Passwords (k-anonymity: só 5 caracteres do SHA-1 saem; `Add-Padding: true`), ligada por padrão e desligável em `Identity:CheckBreachedPasswords`. Se a API estiver fora do ar ou demorar mais de 5 s, a verificação é aberta: a senha é aceita e um aviso vai para o log.
7. **Templates Scriban embutidos** (`EmailTemplates/*.sbn`), com layout comum e todo valor escapado por `html.escape`, porque o nome vem do cadastro.
8. **Endpoints públicos explícitos.** `AllowPublicAccess(motivo)` grava `PublicEndpointAttribute`. `EndpointSecurityConventionTests` falha se algum endpoint `/api/*` não tiver permissão nem motivo público, ou tiver os dois.
9. **Passkeys fora do modelo** até a Etapa 16 (WebAuthn): o `IdentityUserPasskey` do .NET 10 é ignorado, e o modelo não muda com a versão de schema do Identity. Os índices do Identity foram renomeados para snake_case, e o e-mail normalizado é único também no banco.

### Pacotes

| Pacote | Versão | Licença | Uso |
| --- | --- | --- | --- |
| Microsoft.Extensions.Identity.Core / Stores | 10.0.13 | MIT | ASP.NET Core Identity (Domain e Application) |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.13 | MIT | Stores do Identity sobre o EF Core |
| Microsoft.AspNetCore.DataProtection.EntityFrameworkCore | 10.0.13 | MIT | Anel de chaves compartilhado no PostgreSQL |
| Scriban | 7.5.1 | BSD-2-Clause | Templates dos e-mails (seção 9, tarefa 5) |

## Consequências

- O anel de chaves fica **sem criptografia em repouso** no banco. Antes de produção, proteger as chaves com o `IKeyProvider` ou o cofre (seção 7.3); está nas pendências.
- Novas migrations do Identity precisam de `--context IdentityModuleDbContext`, porque o assembly tem dois contextos.
- Rate limit dos endpoints públicos (abuso de envio de e-mails) entra com o rate limit de login no PR B.
- Revogar sessões ao redefinir a senha entra com as sessões do OpenIddict, no PR B.
