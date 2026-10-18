LexFlow — regras para agentes
Leia docs/progress.md e a última entrada de docs/handoff.md antes de começar. Siga a ordem de etapas de docs/spec/implementacao.md.
Stack fixa: .NET 10, EF Core 10 + Npgsql, PostgreSQL 17+, Angular, Wolverine, Hangfire, OpenIddict.
Nuvem: nenhum SDK proprietário fora das implementações de interfaces em Infrastructure (IObjectStorage, IEmailSender, IOcrProvider, IKeyProvider).
Não adicione pacotes sem checar licença e registrar ADR em docs/adr/.

Proibido: MediatR, AutoMapper, MassTransit (licenças), IgnoreQueryFilters fora de Platform,
DateTime.Now (use IClock), SQL concatenado, segredos no código, editar migrations aplicadas.

Todo endpoint: permissão + teste de integração + entra automaticamente nos SecurityTests.
Todo dado de escritório implementa ITenantOwned; dado ligado a processo implementa ICaseScoped.
Handlers retornam Result<T>; erros com código estável (modulo.erro).
Frontend: standalone, OnPush, signals, sem HTTP fora de data/, textos via Transloco.
Comandos: dotnet build, dotnet test, dotnet format, npm run lint, npm test, npx playwright test.
Antes de concluir: rodar tudo acima e atualizar docs/progress.md.
Dúvida de negócio ou ação irreversível: pare e pergunte.
Ao terminar ou interromper um trabalho: acrescente uma entrada no topo de docs/handoff.md (feito, estado, próximos passos, decisões novas) e atualize o "Onde paramos" do AGENTS.md (lido pelo Codex).
