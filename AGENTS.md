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
- Manual de utilização: após cada melhoria, revisar **os dois** arquivos `docs/manual/README.md` e `docs/manual/ambiente-local.md`, atualizando as instruções afetadas de uso e execução local no mesmo trabalho. Quando não houver impacto em um deles, registrar isso na verificação da entrega, sem inventar passos. Descrever apenas o que está entregue e identificar funcionalidades futuras. Antes de encerrar, atualizar também o progress e o handoff para orientar o próximo agente.
- Ambiente local (preferência do usuário em 2026-10-19): deixar a aplicação fechada após as verificações, preservando os volumes/cadastros. Reabrir para exploração manual somente quando o usuário pedir; não manter o AppHost ou seus serviços Docker ligados como cortesia.
- Durante a implementação: execute os testes relacionados à mudança (unitários e, conforme o impacto, integração, segurança ou e2e). Amplie a execução se houver falha, mudança transversal ou dúvida de regressão. Não repita a suíte completa a cada ajuste.
- Comandos obrigatórios no fechamento do PR de implementação: `dotnet build`, `dotnet test`, `dotnet format`, `npm run lint`, `npm test`, `npx playwright test`, com os demais gates da seção "Verificação completa".
- Antes de concluir o PR: complete a verificação integral e registre os resultados em `docs/progress.md`. Testes direcionados durante o desenvolvimento não substituem os gates de fechamento.
- Dúvida de negócio ou ação irreversível: pare e pergunte.
- Documentação, commits, PRs e mensagens ao usuário em português do Brasil. Código, nomes e testes em inglês, como já está.

## Documentação a seguir

Leia nesta ordem antes de planejar qualquer PR:

1. **`docs/progress.md`**: o estado atual de cada etapa e PR, as decisões de negócio já tomadas (com data), as pendências e a última verificação local. É a fonte da verdade sobre onde paramos.
2. **`docs/spec/implementacao.md`**: o guia técnico e a ordem das etapas.
   - §1: como o agente deve usar o documento.
   - §2 a §8: stack, arquitetura, convenções do backend (§4) e do frontend (§5), banco, multi-tenant e migrations (§6), segurança e permissões (§7), ambiente e CI (§8).
   - §9 a §12: as etapas, cada uma com escopo e critérios de aceite. **A Etapa 5 está na §10.**
   - §13: estratégia de testes e qualidade. §14: modelos de prompt e checklist do agente.
3. **`docs/spec/requisitos-v3.md`**: os requisitos de negócio. A seção de cada módulo diz o que construir. Para a Etapa 5: **§7 (Contencioso)**, e também §3 (perfis e permissões) e §14 (requisitos não funcionais).
4. **`docs/adr/`**: as decisões de arquitetura, uma por PR, de 0001 a 0034 (0032 aceita no PR #31; 0033 aceita no PR #32; 0034 proposta no 6C). Antes de mexer numa área, leia as ADRs dela. As de base:
   - 0001 a 0003: monolito modular, stack e módulos por etapa;
   - 0007: pipeline de casos de uso (validação, autorização, transação);
   - 0010: trilha de auditoria;
   - 0011: dados de referência e migrations;
   - 0012: contrato da API e cobertura;
   - 0018 e 0019: tenant, autorização, RLS e SecurityTests;
   - 0025 e 0026: módulo Litigation, numeração, escopo de dados e casos sigilosos.
5. **`docs/openapi/v1.json`**: o contrato da API, gerado pelos testes (não edite à mão).
6. **`tests/LexFlow.LoadTests/README.md`**: como rodar o teste de carga.
7. **`docs/manual/README.md`** e **`docs/manual/ambiente-local.md`**: manual de utilização e guia de inicialização; atualizar quando mudar o uso das telas.

Se a spec e uma decisão registrada no `progress.md` ou numa ADR divergirem, vale a decisão registrada, que é mais recente. Em caso de dúvida, pergunte.

## Onde paramos

- **Leia primeiro a última entrada de `docs/handoff.md`**: é ali que cada agente (Claude Code ou Codex) deixa o que fez, o que ficou combinado com o usuário e o que o próximo deve fazer.
- O estado oficial de cada etapa e de cada PR está no topo de `docs/progress.md`. As decisões de negócio ficam na seção de cada etapa, com data.
- **Etapas 0 a 5: concluídas.**
- **Etapa 5 (processos e casos), encerrada em 2026-10-19:**
  - 5A (#24) e 5B (#25) mergeados;
  - 5C1 (#26) mergeado: índices monetários, `IMonetaryCorrection`, pedidos com valores por instância e a ADR 0027. Atende ao critério 3;
  - 5C2 (#27) mergeado: risco (CPC 25), acordos com rodadas, aprovação, homologação e parcelas, audiências com redesignação, a permissão `cases.risk.assess` e a ADR 0028;
  - 5D (#28) mergeado. Traz a linha do tempo (`ITimelineContributor`), o histórico de situação, a importação XLSX/CSV com ClosedXML (critério 2 atendido) e as operações em lote (ADR 0029);
  - 5E1 (#29) mergeado: `features/cases` (lista, assistente, detalhe básico, linha do tempo, sigilo, numeração), preferências de tela e opções de cadastro (ADR 0030);
  - **5E2 (#30) mergeado pelo usuário em 2026-10-19**: abas Pedidos e valores, Risco, Acordos e Audiências; `/processos/importar`; stores, testes e correção da contagem final de inválidas na importação. ADR 0031 aceita; os três critérios de aceite da Etapa 5 estão atendidos.
- **Etapa 6 (publicações e andamentos):** plano 6A a 6E aprovado em 2026-10-19. **6A (#31) e 6B (#32) concluídos**, aprovados e mergeados pelo usuário em 2026-10-19; ADRs 0032 e 0033 aceitas. O 6B entregou captura fake, deduplicação concorrente, retentativas, monitor/alertas/métrica e notificações in-app/e-mail/SignalR. Branch atual `codex/etapa-6c-datajud-integracoes`, criado da main atualizada (`a137919`). **6C/DataJud implementado e verificado localmente**, com código/documentos em commit local, sem push/PR novo solicitado; fornecedor real de publicações ainda pendente. Conferir resultados/estado no progress/handoff, sem pedir novamente aprovação das escolhas já feitas. Dúvida de negócio nova exige pergunta. Triagem/fechamento no 6D e telas no 6E; Etapa 6 ainda em andamento.
- **Decisões do 5C2 (2026-10-18), já implementadas:**
  - os termos finais do acordo vão à aprovação antes da homologação, e quem tem `cases.settlements.approve` aprova até o próprio acordo;
  - avaliar risco pede `cases.risk.assess`;
  - a redesignação mantém a audiência e guarda o histórico;
  - as parcelas recebem baixa manual até a Etapa 10.
- **Decisões de cálculo já valendo** (ADR 0027):
  - a SELIC não leva juros extras e se acumula por soma;
  - os juros simples são pro rata dia, sobre o valor corrigido;
  - a deflação entra, com piso no valor original.
- **Decisões da importação (2026-10-18), já implementadas:** pessoas fora do cadastro são criadas (o documento existente reaproveita a pessoa); a pasta legada é mantida quando vem na planilha; CNJ existente vai para o relatório; importam-se as válidas.
- **Manual e demonstração (2026-10-19):** manual e guia preservados e incrementados no 6C. Revisar ambos após toda melhoria e manter a continuidade para Claude e Codex. **Preferência mais recente:** aplicação encerrada a pedido do usuário, incluindo os sete serviços Docker desta instância, sem remover volumes/cadastros. Reabrir para exploração manual somente a pedido; não manter serviços ligados após as verificações.
- **6C verificado (2026-10-19):** implementação no commit `dd3bc20`; 794 testes .NET, cobertura acima de 80%, builds/formatação/dependências/modelos, 96 Vitest e duas rodadas de 14 Playwright com acessibilidade. ADR 0034 proposta; resultados no progress. **Push/PR autorizados pelo usuário**, com código e documentos no mesmo envio; confirmar o PR pelo branch `codex/etapa-6c-datajud-integracoes`. AppHost da sessão `47794` e seus contêineres encerrados a pedido; volumes preservados.
- **Próximo trabalho:** concluir a publicação autorizada do 6C e acompanhar CI/revisão; aguardar merge pelo usuário. Depois, fazer a arrumação documental/main/ADR 0034 e preparar o branch do 6D conforme o ciclo. Fornecedor real permanece pendente até contratação/documentação/acesso. Triagem/fechamento ficam no 6D e telas no 6E, após o merge do 6C. Nunca fazer merge pelo usuário.

## Ciclo de trabalho (um PR por vez)

1. Planeje, mostre o plano e espere a aprovação. Dúvida de negócio vira pergunta antes de implementar.
2. Implemente no branch combinado, executando os testes relacionados à mudança durante o desenvolvimento. No fechamento do PR, rode a verificação completa (abaixo).
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
   5. atualize o "Onde paramos" deste arquivo;
   6. faça commit e push dessas mudanças nesse branch.
6. **Sempre que terminar ou interromper um trabalho**, acrescente uma entrada no topo de `docs/handoff.md`: o que fez, o estado (commit, push, PR), o que falta e as decisões novas do usuário. Se o "Onde paramos" mudou, atualize-o aqui também. O `CLAUDE.md` e o `AGENTS.md` têm as mesmas regras: mudou uma, mude a outra.

## Verificação completa

**Política aprovada pelo usuário em 2026-10-19 (vale para Codex e Claude):** durante a implementação, priorizar os testes afetados para obter retorno rápido. No fechamento do PR de implementação, executar todos os comandos e gates abaixo, incluindo cobertura, segurança e Playwright contra o AppHost quando aplicável. Não reduzir cobertura, omitir verificações obrigatórias nem relaxar asserções para economizar processamento.

Depois que as verificações passarem, não repetir sem nova mudança, falha ou preocupação concreta que justifique. Se uma alteração posterior invalidar resultados anteriores, repetir as verificações afetadas e consolidar o estado final antes de concluir. Registrar no progress quais testes foram executados, seus resultados, eventuais limitações e pendências; não declarar a verificação completa com base apenas em testes direcionados.

```bash
dotnet build LexFlow.sln -c Release          # TreatWarningsAsErrors: 0 warnings
dotnet format LexFlow.sln --verify-no-changes
# Testes com cobertura, projeto por projeto (Testcontainers: precisa do Docker Desktop rodando)
for p in tests/*.UnitTests tests/LexFlow.ArchitectureTests tests/*.IntegrationTests tests/LexFlow.SecurityTests tests/LexFlow.AppHost.Tests; do
  dotnet test --project "$p" --no-build -c Release --coverlet --coverlet-output-format cobertura --results-directory "TestResults/$(basename "$p")"
done
python scripts/coverage_gate.py TestResults --minimum 80   # Domain e Application de cada módulo com pelo menos 80%
dotnet list LexFlow.sln package --vulnerable --include-transitive
dotnet ef migrations has-pending-model-changes -p src/Modules/<M>/LexFlow.<M>.Infrastructure -s src/Host/LexFlow.Api -c <M>DbContext   # Platform, Organization, Crm, Litigation, Publications
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
- **E2E em duas rodadas consecutivas:** o limite local de 120 requisições públicas/minuto pode gerar HTTP 429 em `/conta/confirmar-email` quando as duas suítes usam o mesmo IP. Para essa verificação, defina `LexFlow__PublicPermitsPerMinute=10000` no ambiente do AppHost antes de iniciá-lo (configuração de teste já suportada). Preserve o limite de produção; não trate 429 como falha de seletor nem altere o teste para ignorá-lo.
- **AppHost.Tests e Playwright:** o projeto de testes sobe seu próprio AppHost com as mesmas portas locais. Execute-o sem o AppHost manual e só depois inicie a instância usada pelo Playwright. Se reduzir workers por disputa de recursos, registre o comando e preserve os traces das falhas; não relaxe asserções nem ignore acessibilidade.
- **AppHost com `--no-build`:** a CLI do Aspire executa API e Worker em Debug. Depois de mudar o backend, compile também em Debug antes de usar `--no-build`, mesmo que o build Release esteja atualizado. Aguarde o SPA responder HTTP 200 em `:4200` antes do Playwright.
- **API do BCB:** `api.bcb.gov.br` recusa as requisições da máquina de desenvolvimento. Para dados de SELIC e TR, use o Ipeadata. A Calculadora do Cidadão (`www3.bcb.gov.br/CALCIDADAO`) funciona e serve para conferir cálculos.
- **Rotas aninhadas:** num `MapGroup`, `MapGet("/")` ou `MapGet("")` gera rota com barra no fim (`/claims/`). Use o caminho completo no grupo (`/{id:guid}/claims`).
- **Erros de validação:** o FluentValidation responde `validation.failed`, com o código de cada campo em `errors`. Só os erros dos handlers vêm no `code` principal.
- **`DateTimeOffset` no PostgreSQL:** o Npgsql só grava em UTC (`timestamptz`). Normalize com `ToUniversalTime()` no domínio quando a data vem do usuário com fuso.
- **Eventos nos testes:** o `PublishedEvents` da fábrica guarda o evento antes de o publicador preencher `OrganizationId` e `EventId`; não confira esses campos ali.
- **Jobs nos testes de API:** não há Worker. A fábrica troca o `IJobScheduler` por um que guarda os jobs, e o teste os executa com `_factory.RunJobsAsync(orgId)`, que usa o `TenantOverride` do escopo. Serviços como o antivírus são trocados pelo parâmetro `replaceServices` da fábrica, e não pelo `WithWebHostBuilder`, que criaria outro contêiner. O bucket do armazenamento precisa de `IObjectStorageProvisioner.EnsureBucketAsync` no início do teste.
- **Nomes reservados (CA1716):** `Imports`, `Structure` e outros nomes de palavras-chave do VB não servem para namespace nem para membro de interface. Use `CaseImports`, `OrgStructure` etc.
- **Caminhos no CI (Linux):** `Path.GetFileName` não trata `\` como separador fora do Windows. Para nomes de arquivo vindos do usuário, corte no último `/` ou `\` (ver `CaseImport.Start`).
- **Nomes de schema no OpenAPI:** dois records com o mesmo nome em módulos diferentes viram um schema só, e um sobrescreve o outro. Dê nomes únicos aos DTOs de resposta (ex.: `CaseAssignmentResponse`). Para uma correção de descrição que o oasdiff acuse como quebra, registre a exceção em `docs/openapi/oasdiff-ignore.txt`, com a ADR.
- **Listas para telas de negócio:** as listas de estrutura e de membros exigem permissões de configuração. Telas de negócio usam endpoints próprios de opções (ex.: `GET /api/v1/cases/options`, com `cases.read`).
- **E2E com Material stepper:** os passos inativos ficam escondidos, e durante a animação dois "Próximo" ficam visíveis. Clique com `.filter({ visible: true }).last()`.
  - Ao avançar passos consecutivos, aguarde o conteúdo do passo anterior ficar escondido (id de `aria-controls` do cabeçalho expandido) antes do próximo clique. Sem isso, o teste pode clicar outra vez no botão que está saindo.
- **E2E com mat-select vazio:** o rótulo flutuante fica por cima da caixa e intercepta o clique (no CI, de forma permanente). Abra pelo teclado: `getByRole('combobox', { name })`, `focus()` e `press('Enter')`.
