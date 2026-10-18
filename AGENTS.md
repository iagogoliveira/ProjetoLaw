# LexFlow — instruções para agentes (Codex e outros)

Este arquivo tem as mesmas regras do `CLAUDE.md`, mais o que é preciso para continuar o trabalho de onde parou. Se os dois divergirem, vale o `CLAUDE.md`, e o divergente deve ser corrigido.

## Regras do projeto

- Leia `docs/progress.md` antes de começar. Siga a ordem de etapas de `docs/spec/implementacao.md`. Os requisitos estão em `docs/spec/requisitos-v3.md`.
- Stack fixa: .NET 10, EF Core 10 + Npgsql, PostgreSQL 17+, Angular, Wolverine, Hangfire, OpenIddict.
- Nuvem: nenhum SDK proprietário fora das implementações de interfaces em Infrastructure (`IObjectStorage`, `IEmailSender`, `IOcrProvider`, `IKeyProvider`).
- Não adicione pacotes sem checar a licença e registrar uma ADR em `docs/adr/`.
- Proibido:
  - MediatR, AutoMapper, MassTransit (licenças);
  - `IgnoreQueryFilters` fora do Platform;
  - `DateTime.Now` e `DateTime.UtcNow` (use `IClock`; nos testes da API, o `IClock` da fábrica);
  - SQL concatenado (use SQL interpolado e parametrizado do EF);
  - segredos no código;
  - editar migrations já aplicadas.
- Todo endpoint tem permissão, teste de integração e entra automaticamente nos SecurityTests.
- Todo dado de escritório implementa `ITenantOwned`. Dado ligado a processo implementa `ICaseScoped`.
- Handlers retornam `Result<T>`. Erros têm código estável (`modulo.erro`).
- Frontend: componentes standalone, OnPush e signals; sem HTTP fora de `data/`; textos via Transloco (pt-BR e en com as mesmas chaves).
- Comandos obrigatórios: `dotnet build`, `dotnet test`, `dotnet format`, `npm run lint`, `npm test`, `npx playwright test`.
- Antes de concluir: rode tudo acima e atualize `docs/progress.md`.
- Dúvida de negócio ou ação irreversível: pare e pergunte.
- Documentação, commits, PRs e mensagens ao usuário em português do Brasil. Código, nomes e testes em inglês, como já está.

## Onde paramos

- O estado de cada etapa e de cada PR está no topo de `docs/progress.md`. As decisões de negócio de cada etapa ficam na seção dela, com data.
- **Etapas 0 a 4: concluídas.**
- **Etapa 5 (processos e casos):**
  - 5A (#24) e 5B (#25) mergeados;
  - **o próximo é o PR 5C**, no branch `etapa-5c-valores-risco`, que já existe com o commit de progresso.
- **Escopo do 5C** (plano aprovado; ver "Plano (5 PRs)" na seção da Etapa 5 do progresso):
  - **pedidos:** `Claim`, ligado à tabela auxiliar "Objetos/pedidos", e `ClaimValuation` por instância (valor pedido, estimado e de condenação), com histórico das alterações;
  - **risco:** `RiskAssessment` por processo e por pedido (provável, possível ou remoto, CPC 25);
  - **acordos:** `Settlement` (proposta, contraproposta, homologação) com `SettlementInstallment`. A aprovação usa `cases.settlements.approve`;
  - **audiências:** `Hearing`;
  - **índices monetários:**
    - tabela `platform.monetary_indexes` (índice, mês, valor) com seed de IPCA-E, INPC, SELIC e TR;
    - job mensal (Hangfire) que importa das APIs públicas do BCB (SGS) e do IBGE (SIDRA), com fonte falsa em dev e nos testes;
  - **correção:** `IMonetaryCorrection`, que corrige pelo índice e aplica juros simples em % ao mês a partir de uma data (decisão de 2026-10-18; a regra da Lei 14.905/2024 fica para depois). Testado contra **10 cálculos conferidos à mão**, que é o critério 3 da Etapa 5.
- **Depois do 5C:**
  - 5D: linha do tempo (`ITimelineContributor`), importação XLSX/CSV com ClosedXML (critério 2) e operações em lote;
  - 5E: frontend e e2e dos processos.

## Ciclo de trabalho (um PR por vez)

1. Planeje, mostre o plano e espere a aprovação. Dúvida de negócio vira pergunta antes de implementar.
2. Implemente no branch combinado, com a verificação completa (abaixo).
3. Registre no `docs/progress.md`:
   - a seção do PR, com tabela de subtarefas e os testes de cada uma;
   - as decisões de negócio;
   - a linha "Última verificação local";
   - as pendências.
   Escreva também uma ADR nova, com status "Proposta (para revisão no PR …)".
4. Faça o commit. Push e PR só quando o usuário pedir. **Quem faz o merge é o usuário.**
5. Depois do merge:
   1. atualize a `main`;
   2. crie o branch do próximo PR;
   3. no `progress.md`, marque o PR como concluído, com o número e a data;
   4. marque a ADR como "Aceita (PR #N mergeado em …)";
   5. faça commit e push dessas mudanças nesse branch.

## Verificação completa

```bash
dotnet build LexFlow.sln -c Release          # TreatWarningsAsErrors: 0 warnings
dotnet format LexFlow.sln --verify-no-changes
# Testes com cobertura, projeto por projeto (Testcontainers: precisa do Docker Desktop rodando)
for p in tests/*.UnitTests tests/LexFlow.ArchitectureTests tests/*.IntegrationTests tests/LexFlow.SecurityTests tests/LexFlow.AppHost.Tests; do
  dotnet test --project "$p" --no-build -c Release --coverlet --coverlet-output-format cobertura --results-directory "TestResults/$(basename "$p")"
done
python scripts/coverage_gate.py TestResults --minimum 80   # Domain e Application de cada módulo com pelo menos 80%
dotnet list LexFlow.sln package --vulnerable --include-transitive
dotnet ef migrations has-pending-model-changes -p src/Modules/<M>/LexFlow.<M>.Infrastructure -s src/Host/LexFlow.Api -c <M>DbContext   # Platform, Organization, Crm, Litigation
cd src/Web/lexflow-web && npm run lint && npm test && npx ng build && npx playwright test
```

- **Playwright com telas:** quando o PR mexe em telas, rode também contra o AppHost:
  - suba com `dotnet run --project src/Aspire/LexFlow.AppHost`;
  - o SPA fica em `:4200`, o Mailpit em `:8025` e a API em `:5238`;
  - rode `E2E_BASE_URL=http://localhost:4200 E2E_MAILPIT_URL=http://localhost:8025 npx playwright test`, de preferência duas vezes seguidas.
- **Contrato OpenAPI:** depois de mudar endpoints, rode `LEXFLOW_UPDATE_OPENAPI=1 dotnet test --project tests/LexFlow.Api.IntegrationTests -- --filter-class "*OpenApiContractTests"` e em seguida `npm run generate:api` em `src/Web/lexflow-web`.
  - O CI roda oasdiff com `--fail-on ERR`, então mudanças de contrato devem ser só adições.
- **Migration nova:** `dotnet ef migrations add <Nome> -p src/Modules/<M>/LexFlow.<M>.Infrastructure -s src/Host/LexFlow.Api -c <M>DbContext -o Persistence/Migrations`.
  - Tabela nova de escritório precisa de `Security.EnableTenantIsolation` no `Up` e de `DisableTenantIsolation` no `Down`. O `RowLevelSecurityTests` confere.
  - `migrations remove` exige banco. Se a migration ainda não foi commitada, apague os arquivos dela e restaure o snapshot pelo git.
- **Teste de carga:** `bash tests/LexFlow.LoadTests/run-cases-list.sh`, com o AppHost no ar (k6 no Docker).

## Armadilhas conhecidas

- **Filhas de agregado com Id gerado no domínio** (`Guid.CreateVersion7()`): configure `builder.Property(x => x.Id).ValueGeneratedNever()`. Sem isso, uma filha nova numa edição vira alteração e a gravação falha (ADR 0026).
- **Rota nova com parâmetro:** precisa de um caso em `CrossTenantCases`, em `tests/LexFlow.SecurityTests/EndpointSecurityTests.cs`.
  - O corpo enviado precisa passar na validação, para o 404 vir do recurso de outra organização.
  - Upload multipart: leia o formulário da requisição em vez de usar `IFormFile` no parâmetro, senão o roteamento responde 415 antes da autenticação.
- **Filtro de enum na query:** use `EnumQuery.Parse<T>(valor, erroDoMódulo)` e `.DocumentEnumQuery<T>("param")`, do Web.Common.
- **Comunicação entre módulos:** só por contratos (`*.Contracts`, ou `Application.Abstractions`), nunca por outro DbContext. Exemplos: `IPeopleDirectory`, `ICourtDirectory`, `IOrganizationDirectory`, `ILookupItemValidator`, `ICaseVisibility`.
- **Acesso a processos:** novos casos de uso de processo precisam de `IResourceAuthorizer`, que chama `CaseAccess.AuthorizeAsync` com a permissão do endpoint. Ver `Litigation.Application/Access`.
- **Frontend (signalStore):** um método com o mesmo nome de um campo do estado sobrescreve o signal. Downloads usam `responseType: 'blob'`, porque o cliente gerado roda sobre o HttpClient do Angular.
- **Analisadores** (Meziantou/CA, o build quebra):
  - método com mais de 60 linhas ou 40 instruções (MA0051): extraia métodos;
  - array constante como argumento (CA1861): use campo `static readonly`;
  - `==` entre strings (MA0006): use `string.Equals` com comparação explícita;
  - `using` fora de ordem: rode `dotnet format`.
- **Token de acesso:** é criptografado (JWE) e não dá para ler. O OpenIddict confere o emissor pelo host da requisição.
- **Testes de integração:** dependem do Docker Desktop rodando. Se tudo falhar em 0 ms, o Docker parou.
