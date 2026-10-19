# ADR 0035 — Triagem, transação local compartilhada e fechamento diário

- **Status:** Proposta (para revisão no PR 6D)
- **Data:** 2026-10-19
- **Etapa:** 6 (PR 6D)

## Contexto

O plano 6A–6E, aprovado em 2026-10-19, exige salvar a triagem e o prazo, tarefa ou audiência criado na mesma transação, com rollback conjunto. O outbox assegura entrega posterior, mas não torna duas gravações independentes atômicas. A ADR 0007 abre uma transação por módulo; a exceção local abaixo foi aprovada pelo usuário e é detalhada antes da implementação.

## Decisão técnica

1. Comandos que precisam dessa atomicidade implementam `ILocalTransactionCommand` e declaram explicitamente os módulos participantes, começando pelo módulo do comando. Os demais comandos mantêm o pipeline da ADR 0007. Validação e autorização continuam antes da abertura da transação; comandos internos preservam seus próprios validadores e autorizadores.
2. Um coordenador scoped de Infrastructure.Common resolve as unidades de trabalho registradas pelos módulos. Não conhece seus tipos concretos nem expõe entidades/DbContexts ao módulo chamador. Publications continua chamando Scheduling/Litigation exclusivamente por Contracts.
3. Todos os participantes devem usar o mesmo PostgreSQL e o mesmo escritório, com conexões fechadas, sem alterações pendentes e sem transação própria aberta. O coordenador abre uma transação no contexto principal e inscreve os demais sobre a mesma `DbConnection` e a mesma `DbTransaction`, com `UseTransactionAsync`. Não há `TransactionScope`, transação distribuída nem segundo commit. A instância não permite outra coordenação simultânea no mesmo escopo.
4. Handlers internos enxergam a transação já aberta e não a confirmam. Sucesso confirma uma vez; `Result` de falha, exceção, cancelamento ou descarte sem commit desfazem todos os participantes. Auditoria e envelopes de outbox permanecem nessa transação. A organização é conferida novamente antes do commit. O contexto não deve trocar de tenant dentro de uma operação compartilhada.
5. Ao terminar, os contextos secundários se desligam da transação e recebem conexões próprias do mesmo `NpgsqlDataSource`. `SetDbConnection` descarta a conexão anteriormente possuída pelo EF, portanto não se guarda uma conexão descartada para restaurá-la depois. No rollback, os change trackers dos participantes são limpos para não deixar entidades aceitas em memória que não existem mais no banco. Os contextos podem continuar sendo usados no escopo após a limpeza.
6. Nenhum pacote novo. Reutilizam-se EF Core, Npgsql e os interceptors de tenant, auditoria e outbox existentes. A coordenação é restrita aos comandos optantes; outras integrações continuam usando outbox conforme a ADR 0007.

## Regras de negócio já aprovadas

- Somente núcleo mínimo de Scheduling nesta entrega, com responsável e datas confirmados por humano. Calculadora, feriados e agenda completa permanecem na Etapa 7; telas de publicações no 6E.
- Fechamento com `publications.close`, por recebimento em America/Sao_Paulo, incluindo pendências anteriores e bloqueando conclusão quando faltar captura esperada. Escritório individual não exige segunda pessoa.
- **Resposta adicional do usuário em 2026-10-19:** permitir encerrar uma publicação irrelevante ou capturada por engano sem vínculo com processo, exigindo justificativa e registrando quem decidiu. Criar prazo, tarefa ou audiência exige processo vinculado. Essa decisão não dispensa a autorização e o sigilo da publicação.

## Perguntas complementares ainda pendentes

1. Se novas publicações chegarem depois de um fechamento no mesmo dia, preservar o fechamento no histórico e exigir novo fechamento após as novidades (sugestão enviada ao usuário), ou levá-las ao próximo fechamento.
2. Se criar o destino deve exigir também `schedule.deadlines.manage`, `schedule.tasks.manage` ou `cases.edit` para audiência, além de `publications.triage` (sugestão enviada), ou somente a permissão de triagem.

Não implementar essas escolhas antes das respostas. A infraestrutura genérica de transação independe delas. Este registro ainda não representa implementação ou verificação integral do 6D.

## Verificação prevista

Testes do pipeline optante e do pipeline existente; PostgreSQL real com contextos distintos, commit/rollback, exceção/cancelamento, restauração das conexões, rejeição de tenant ausente/divergente e transações independentes; outbox real com entrega apenas após commit. No fluxo final: triagem/destino/auditoria juntos, concorrência e reexecução, permissões/escopo/sigilo, RLS, migrations e OpenAPI aditivo. Registrar resultados efetivamente executados em `docs/progress.md`.

## Referências técnicas

- [EF Core — transações entre contextos](https://learn.microsoft.com/en-us/ef/core/saving/transactions#cross-context-transaction): compartilhar conexão e transação, inscrevendo os contextos com `UseTransactionAsync`.
- [EF Core 10.0.13 — RelationalConnection.SetDbConnection](https://github.com/dotnet/efcore/blob/v10.0.13/src/EFCore.Relational/Storage/RelationalConnection.cs): propriedade e descarte da conexão ao substituí-la.
