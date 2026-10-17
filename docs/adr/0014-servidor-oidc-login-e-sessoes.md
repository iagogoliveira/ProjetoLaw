# ADR 0014 — Servidor OIDC, login e sessões

- **Status:** Aceita
- **Data:** 2026-10-16
- **Etapa:** 2 (PR B1)

## Contexto

A Etapa 2 pede OpenIddict com o cliente `lexflow-web` (PKCE) e os escopos `openid profile email offline_access api`, login em páginas server-side (o login fica no servidor de identidade, não na SPA), refresh token rotativo em que o reuso de um token antigo revoga a família, bloqueio na 6ª tentativa e sessões listáveis e revogáveis. As decisões de negócio estão no `progress.md`: refresh com 14 dias sem uso e no máximo 30, e MFA opcional por usuário (PR B2).

## Decisão

1. **OpenIddict 7.7.2 em modo passthrough** na API (`/connect/authorize`, `/connect/token`, `/connect/logout`). Entram só os pacotes de servidor, validação e EF Core, sem os de cliente OIDC. As tabelas ficam no schema `identity` (`openiddict_*`). Autorizações e tokens ficam fora da auditoria, por volume e por serem segredo; o segredo e as chaves de cliente são marcados como sensíveis.
2. **Validação local como esquema padrão da API**, no lugar do esquema provisório da ADR 0008. Os endpoints com `RequirePermission` passam a aceitar o access token sem mudar nada neles.
3. **Tokens:**
   - access e id token de 10 min (seção 5.3);
   - refresh token rotativo, válido por 14 dias desde o último uso;
   - **tolerância de reuso zero:** qualquer reuso de refresh token já trocado revoga todos os tokens da autorização. O custo é que uma resposta de renovação perdida na rede encerra a sessão; o SPA (PR C) não pode renovar em paralelo.
4. **Sessão = autorização OIDC.** O authorize cria a autorização permanente e a `Session` (com IP e user agent), e o token leva a claim `sid`. Toda troca no endpoint de token exige a sessão ativa: não revogada, dentro de 14 dias sem uso e de 30 dias de idade. Cada troca renova a janela de inatividade. Logout pelo `id_token_hint`, e também a redefinição de senha, encerram a sessão e revogam os tokens.
5. **O cookie de login dura só até o código.** A página de login grava um cookie de 15 min (`lexflow.login`), e o authorize o apaga ao emitir o código. Não existe uma segunda sessão "do servidor de identidade" paralela à dos tokens: um novo authorize pede a senha de novo e `prompt=none` responde `login_required`.
6. **Páginas Razor em pt-BR** no projeto `Identity.Endpoints`, que vira uma Razor Class Library: entrar, cadastro, confirmar e-mail, esqueci a senha e redefinir senha, nos caminhos dos links dos e-mails. As páginas chamam os mesmos comandos da API. Os acentos saem como texto (codificador com todos os intervalos Unicode); o escape de marcação continua.
7. **Bloqueio:** 5 senhas erradas seguidas bloqueiam por 15 min, e a 6ª tentativa, mesmo com a senha certa, é recusada. E-mail não confirmado não entra.
8. **Rate limit por IP** (janela fixa, 10/min por padrão, `Identity:PublicPermitsPerMinute`) nas páginas de conta e em `/api/v1/auth/*`. O endpoint de token fica de fora, porque escritórios atrás do mesmo IP renovam tokens o tempo todo. Resposta 429 com o código `http.too_many_requests`.
9. **Chaves de assinatura e criptografia:**
   - em produção, certificados PFX em Base64 do cofre (`Identity:Server:SigningCertificate`, `EncryptionCertificate` e `CertificatePassword`), e a API não sobe sem eles;
   - em Development, os certificados de desenvolvimento do OpenIddict;
   - nos testes, chaves efêmeras (`Identity:Server:UseEphemeralKeys`).
   HTTPS só deixa de ser exigido em Development.
10. **Cliente `lexflow-web`** cadastrado pelo MigrationService por um `IDatabaseSeeder` (cria ou atualiza): público, PKCE obrigatório, consentimento implícito, endereços em `Identity:WebClient` (padrão `http://localhost:4200/auth/callback`).
11. **Limpeza diária** (Hangfire no Worker, 03:30) de tokens e autorizações inválidos com mais de 30 dias.

### Pacotes

| Pacote | Versão | Licença | Uso |
| --- | --- | --- | --- |
| OpenIddict.Server.AspNetCore | 7.7.2 | Apache-2.0 | Servidor OIDC |
| OpenIddict.Validation.AspNetCore / ServerIntegration | 7.7.2 | Apache-2.0 | Validação local dos access tokens |
| OpenIddict.EntityFrameworkCore | 7.7.2 | Apache-2.0 | Stores no PostgreSQL |

## Consequências

- **Client Credentials** (API pública e integrações, seção 7.1) entra na Etapa 14, quando houver um cliente que precise dele.
- **Proxy reverso:** atrás de proxy, o IP do rate limit e o esquema HTTPS dependem de `ForwardedHeaders`, a configurar no deploy (pendência).
- **Reuso de refresh token:** o OpenIddict revoga os tokens, mas a linha em `sessions` continua sem `revoked_at`. Ela deixa de valer na próxima troca, que é recusada. Marcar a sessão como revogada nesse caso fica para o PR B2, com a listagem de sessões.
- **Mensagens do login:** a de "confirme seu e-mail" e a de conta bloqueada revelam que o e-mail tem conta. O ASP.NET Core Identity checa isso antes da senha. Fica aceito, porque o rate limit reduz a sondagem.
- Nos testes de integração, cada fábrica da API tem chaves efêmeras próprias: um token só vale na instância que o emitiu.
