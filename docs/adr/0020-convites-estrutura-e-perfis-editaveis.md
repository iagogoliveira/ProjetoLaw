# ADR 0020 — Convites, membros, estrutura e perfis editáveis

- **Status:** Aceita (PR #14 mergeado em 2026-10-17)
- **Data:** 2026-10-17
- **Etapa:** 3 (PR 3D)

## Contexto

A Etapa 3 pede convites por e-mail com token de uso único e validade de 7 dias (tarefa 8), o cadastro da estrutura da seção 3.1 (filiais, áreas, equipes, centros de custo e sócios) e perfis que o escritório possa editar. Quem abre um convite ainda não é membro da organização, então não há tenant ativo; com a RLS do PR 3C, nenhuma tabela de escritório fica visível para essa pessoa.

Decisões de negócio confirmadas em 2026-10-17:
- quem ainda não tem conta se cadastra pelo link do convite, com o e-mail já preenchido;
- só a conta cujo e-mail **confirmado** é o e-mail convidado pode aceitar;
- os perfis (com escopo de dados) são escolhidos no convite, e o membro já entra com eles.

## Decisão

1. **Convite (`org.invitations` e `org.invitation_roles`, com RLS):**
   - guarda o e-mail normalizado, quem convidou, a situação (pendente, aceito, cancelado), o prazo e os perfis;
   - "expirado" não é gravado: é um pendente com o prazo vencido, e aparece assim na listagem;
   - não pode haver dois convites pendentes e válidos para o mesmo e-mail, nem convite para quem já é membro;
   - os perfis e escopos passam pelas mesmas regras da atribuição de perfis (perfis da organização, escopo apontando para a estrutura dela).
2. **Token:**
   - 32 bytes aleatórios em Base64Url. No banco fica só o SHA-256;
   - como nos e-mails de conta (ADR 0013), o token nasce no Worker, na hora do envio. A API publica `InvitationEmailRequestedIntegrationEvent` sem segredo, e o consumidor grava o hash no mesmo commit do registro de processamento. Se esse commit falhar depois do envio, a reentrega manda outro e-mail com outro token, e o primeiro nunca chega a valer;
   - o reenvio recomeça os 7 dias e invalida o link anterior; o cancelamento e o aceite apagam o token (uso único).
3. **Diretório de tokens (`org.invitation_tokens`, sem tenant):** é um dado da plataforma, como o `user_organizations`, com hash, convite, organização, e-mail e prazo. É por ele que o convite é achado antes de haver organização ativa:
   - `POST /api/v1/invitations/preview` é público, com o limite por IP dos endpoints públicos (`PublicRateLimit`, antes `identity-public`). Mostra escritório, e-mail e prazo. Token desconhecido, usado, cancelado ou vencido responde 404, sem distinção;
   - `POST /api/v1/invitations/accept` exige login e a nova permissão universal `organizations.invitations.accept`. Confere o e-mail da conta pelo `IUserDirectory` e só então entra na organização com `TenantOverride.BeginForInvitationAcceptance`, o segundo uso permitido do override (o primeiro é o onboarding). Cria o membro, as atribuições do convite e a linha do diretório de organizações.
   - O token vai sempre no corpo, nunca na URL da API, para não aparecer em logs de acesso. O link do e-mail aponta para o SPA (`Organization:AppUrl` + `/convite?token=…`); a página entra no PR 3F.
4. **Contas para outros módulos:** `IUserDirectory` (Identity.Contracts, implementado no Identity.Infrastructure) devolve id, e-mail, nome e se o e-mail foi confirmado, e nada além disso. A lista de membros passa a trazer nome e e-mail. A página `/conta/cadastro` aceita `?email=` para o cadastro a partir do convite.
5. **Membros:** `DELETE /api/v1/organization/members/{id}` faz exclusão lógica. O membro sai das equipes e do diretório de organizações e perde o acesso na próxima requisição (cache de pertença invalidado). A regra do último Administrador do escritório vale também aqui. Para voltar, a pessoa recebe um convite novo.
6. **Estrutura:** listar, criar, editar e excluir filiais, áreas, equipes (e seus membros), centros de custo e sócios, tudo com `settings.structure.manage`:
   - nome único no escritório (409 `organization.name_taken`), e código do centro de custo único e em maiúsculas;
   - CNPJ da filial validado;
   - a equipe só aponta para área e filial do próprio escritório;
   - a soma das participações dos sócios não passa de 100%;
   - a exclusão é lógica, e barrada (422 `organization.structure_in_use`) enquanto o item for usado por equipe, por escopo de perfil atribuído ou por convite pendente.
7. **Perfis editáveis:**
   - criar, com nome único e a matriz inicial;
   - renomear, inclusive os padrão;
   - excluir: barrado para o Administrador do escritório e para perfil atribuído ou previsto em convite pendente;
   - `GET /api/v1/permissions` entrega o catálogo atribuível por grupo, para a matriz da tela.

## Consequências

- **Rotas e testes:** 32 operações novas no contrato OpenAPI, todas com permissão. Cada rota com id ganhou caso entre organizações nos SecurityTests.
- **Pacotes:** nenhum pacote novo. O Organization.Infrastructure passa a usar o Scriban, já aprovado na ADR 0002, para o template do convite.
- **Pendências:**
  - a leitura da estrutura usa `settings.structure.manage`. Quando os formulários de outros módulos (Etapa 5 em diante) precisarem escolher equipe ou área, será preciso uma permissão de leitura, ou que a lista venha do próprio módulo;
  - a página do convite no SPA (consulta, "entrar" ou "criar conta" com o e-mail preenchido, aceite) e o e2e "cadastro → criar escritório → convidar → aceitar" entram no PR 3F.
