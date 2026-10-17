# ADR 0015 — MFA e "minha conta"

- **Status:** Aceita
- **Data:** 2026-10-16
- **Etapa:** 2 (PR B2)

## Contexto

Decisões de negócio de 2026-10-16: o MFA é opcional por usuário, e quem ativa escolhe TOTP ou código por e-mail como segundo fator; não há "lembrar este dispositivo". A Etapa 2 pede `POST /api/v1/me/mfa/totp`, `GET /api/v1/me` e `GET|DELETE /api/v1/me/sessions`. O MFA obrigatório por organização fica para a Etapa 3.

## Decisão

1. **TOTP em duas etapas.**
   - `POST /api/v1/me/mfa/totp` gera uma chave nova e devolve a chave e o link `otpauth://` (o QR code é gerado no SPA, PR C).
   - `POST /api/v1/me/mfa/totp/confirm` exige um código válido do aplicativo. Só então liga o MFA e devolve **10 códigos de recuperação** de uso único.
   - Com MFA já ligado, as duas respondem 409 (`identity.mfa_already_enabled`), então a chave não pode ser trocada por quem só tem o access token.
2. **Código por e-mail.** `POST /api/v1/me/mfa/email` liga o método sem etapa extra, porque o e-mail já foi confirmado no cadastro. A cada login, a página de login pede o código ao Worker pelo outbox (`AccountEmailKind.SignInCode`), como os demais e-mails de conta. O código é o TOTP do ASP.NET Core Identity derivado do security stamp: o Worker gera, a API valida, e nada secreto passa pela fila. Ele vale por poucos minutos, conforme a janela do provedor do Identity.
3. **Códigos de recuperação só para TOTP.** Quem usa e-mail como segundo fator já depende do e-mail para recuperar a senha.
4. **Login em duas páginas.** Com MFA, `/conta/entrar` leva a `/conta/segundo-fator`. Códigos errados contam para o bloqueio por tentativas, como a senha. Nos códigos numéricos, espaços e hífens são removidos; nos de recuperação, que têm hífen, não.
5. **`amr` no token:** `["pwd"]`, ou `["pwd", "mfa"]` quando houve segundo fator. A Etapa 3 usa isso para exigir MFA por organização.
6. **Sessões listadas** (`GET /api/v1/me/sessions`):
   - entram só as que ainda podem ser usadas: não revogadas, dentro dos prazos e **com refresh token válido**;
   - a sessão cujo refresh foi reutilizado some da lista (o OpenIddict revogou a família), mesmo sem `revoked_at`;
   - `current` marca a sessão do token que fez a consulta (claim `sid`).
   `DELETE /api/v1/me/sessions/{id}` encerra só sessões do próprio usuário; para as de outra pessoa a resposta é 404.
7. **Permissões da própria conta:** `identity.profile.read` (perfil e sessões) e `identity.profile.manage` (encerrar sessão, MFA). Na Etapa 3, o `PermissionCatalog` precisa concedê-las a todo usuário autenticado, independentemente do perfil no escritório.

## Consequências

- **Não há como desligar o MFA nem trocar o método** por si mesmo: isso exige reautenticação (*step-up*) e não está na especificação. Fica como pendência. Hoje só a administração da plataforma resolveria um aparelho perdido, e para TOTP existem os códigos de recuperação.
- O SPA (PR C) monta o QR code a partir do `authenticatorUri` e mostra os códigos de recuperação uma única vez.
