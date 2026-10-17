# ADR 0016 — Autenticação no SPA e "minha conta"

- **Status:** Aceita
- **Data:** 2026-10-16
- **Etapa:** 2 (PR C)

## Contexto

A seção 5.3 pede Authorization Code + PKCE com `angular-auth-oidc-client` contra o OpenIddict, access token curto, refresh rotativo e "nada em `localStorage`". O login e o cadastro ficam nas páginas Razor do servidor de identidade (ADR 0014). Decisão de 2026-10-16: tokens em `sessionStorage`.

## Decisão

1. **`angular-auth-oidc-client` 22.0.1** (MIT):
   - `useRefreshToken`, sem iframe, com renovação 30 s antes de expirar;
   - `useRefreshTokenLock`: o servidor não tolera reuso de refresh token, então a renovação é serializada entre abas;
   - `autoUserInfo: false`: nome e e-mail vêm no id token;
   - o interceptor anexa o token só às chamadas para `apiUrl`.
2. **Tokens em `sessionStorage`** (`DefaultSessionStorageService`, registrado explicitamente): sobrevivem ao F5 na mesma aba e somem ao fechar a aba. Nada vai para `localStorage`.
3. **Configuração em tempo de execução:** o `public/config.json` (`apiUrl`, `authority`) é lido antes do bootstrap. O deploy troca o arquivo sem recompilar o SPA, e o cliente gerado da API usa `apiUrl` como base.
4. **Rotas:**
   - `/entrar` é pública: "Entrar" inicia o authorize, e "Criar conta" leva à página Razor de cadastro;
   - `/auth/callback` conclui o login;
   - as demais rotas ficam sob o shell, com o guard `canMatch`.
   - Se o servidor de identidade estiver fora do ar, o SPA sobe sem login, em vez de falhar no bootstrap.
5. **"Minha conta"** (`features/account`): perfil, verificação em duas etapas (QR code do TOTP, chave para digitar, confirmação e códigos de recuperação mostrados uma única vez, ou código por e-mail) e sessões abertas com "Encerrar".
   - O estado fica numa SignalStore (`@ngrx/signals` 22.0.1, MIT).
   - O HTTP fica só em `data/` (`AccountApi`, sobre o cliente gerado).
   - Os erros chegam pelo `code` do ProblemDetails e são traduzidos em `errors.*`.
6. **QR code** com `qrcode-generator` 2.0.4 (MIT, sem dependências), como imagem GIF em data URL, que o sanitizador do Angular aceita sem `bypassSecurityTrust`. O pacote `qrcode` foi descartado por trazer `yargs` e `pngjs` para a CLI.
7. **CORS na API** só para as origens do cliente `lexflow-web` (seção 7.3), sem credenciais de cookie.
8. **E2E do critério de aceite** (`e2e/account-flow.spec.ts`) contra o AppHost:
   - o Mailpit fica em porta fixa (8025) e o teste lê nele o link de confirmação;
   - o código TOTP é gerado no teste, e o axe roda em cada tela do SPA;
   - sem `E2E_MAILPIT_URL`, o teste é pulado: localmente, contra o `ng serve`, só a página pública é testada;
   - no AppHost, o rate limit das páginas de conta vai para 120/min (`LexFlow:PublicPermitsPerMinute`), porque o fluxo completo passa de 10 requisições por minuto no mesmo IP. A produção continua com 10.

### Pacotes (npm)

| Pacote | Versão | Licença | Uso |
| --- | --- | --- | --- |
| angular-auth-oidc-client | 22.0.1 | MIT | OIDC (PKCE, refresh rotativo) |
| @ngrx/signals | 22.0.1 | MIT | SignalStore por feature (seção 5) |
| qrcode-generator | 2.0.4 | MIT | QR code do TOTP |

## Consequências

- Um XSS consegue ler os tokens da aba, como aconteceria com tokens em memória. A CSP restritiva da seção 7.3, que ainda falta configurar, é a defesa principal.
- Abrir o LexFlow em outra aba pede login de novo, porque o `sessionStorage` é por aba.
- A renovação de token não tem e2e próprio: o access token dura 10 min. A rotação e a revogação por reuso são provadas nos testes de integração da API.
