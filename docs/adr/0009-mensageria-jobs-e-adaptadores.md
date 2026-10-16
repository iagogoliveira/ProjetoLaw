# ADR 0009 — Mensageria (outbox/inbox), jobs e adaptadores externos

- **Status:** Aceita
- **Data:** 2026-10-16
- **Etapa:** 1

## Contexto

A seção 4.5 pede eventos de integração entre módulos pelo outbox do Wolverine, com consumo idempotente. A seção 2.1 pede Hangfire para jobs. A seção 8.3 pede adaptadores para armazenamento, e-mail e antivírus. O critério de aceite da Etapa 1 exige que um evento gravado no outbox seja publicado e consumido uma única vez, mesmo com reentrega.

## Decisão

### Publicação (outbox)

- **Publicar:** o handler chama `IIntegrationEventPublisher.Publish(evento)`. O publicador preenche `EventId` (UUID v7), `OrganizationId` (do tenant), `OccurredAt` e `CorrelationId`, e deixa o evento pendente no escopo.
- **Gravar:** no próximo `SaveChanges` de um módulo, o `OutboxInterceptor` faz `IDbContextOutbox.Enroll(dbContext)` e publica os pendentes. Os envelopes vão para `wolverine.wolverine_outgoing_envelopes`, na mesma transação dos dados. As tabelas do Wolverine são mapeadas em todo DbContext (`MapWolverineEnvelopeStorage`) e ficam fora das migrations, porque o Wolverine mantém o próprio schema.
- **Enviar:** o envio é liberado só depois do commit. Isso acontece no fim do `SaveChanges` ou, se houver transação explícita (decorator de transação), no `TransactionCommitted`. Em rollback, os envelopes somem junto com os dados.

### Consumo (inbox)

- **Inbox durável** do Wolverine em todos os listeners, que descarta a reentrega do mesmo envelope.
- **Idempotência por evento:** `IntegrationEventConsumer<TEvent, TDbContext>` registra `(event_id, consumer)` em `{schema}.processed_integration_events`, no mesmo `SaveChanges` do efeito. Se o mesmo evento chegar de novo, mesmo como envelope novo, ele é ignorado (`OnDuplicateSkipped`). A chave primária protege contra corrida.
- **Tenant:** o consumidor executa com o tenant e a correlação do evento (`AmbientContext`).

### Wolverine

- Usado só para mensageria. A descoberta por convenção fica desligada (os `*Handler` de comando seriam confundidos com handlers de mensagem), e os consumidores (`IIntegrationEventHandler<T>`) são registrados explicitamente.
- Transporte RabbitMQ com roteamento por tipo de mensagem. Persistência no PostgreSQL, schema `wolverine`.
- `WolverineFx.RuntimeCompilation`: o Wolverine 6 separou o compilador de runtime.
- `ServiceLocationPolicy.AlwaysAllowed`: os consumidores recebem o DbContext do módulo, registrado por fábrica.

### Jobs (Hangfire)

- `IJobScheduler` com PostgreSQL (schema `hangfire`). A API só agenda; o servidor roda no Worker.
- O `JobContextFilter` grava o tenant e a correlação de quem agendou como parâmetros do job e os restaura na execução (seção 6.2: "em jobs, a partir do evento").
- Jobs recorrentes de plataforma rodam sem tenant, no fuso America/Sao_Paulo.

### Adaptadores

- `IObjectStorage` (S3): chave `/{org}/{modulo}/{id}`, URL de download com validade de 5 min e URL de upload direto.
- `IEmailSender`: SMTP com MailKit.
- `IFileScanner`: cliente próprio do protocolo clamd (`zINSTREAM`, cerca de 60 linhas). Assim não entra uma dependência como o nClam, e o protocolo fica sob nosso controle.

## Consequências

- **Efeito único comprovado** por `OutboxTests` (Wolverine, PostgreSQL e RabbitMQ reais): publicação e consumo únicos, reentrega descartada, rollback sem publicação, tenant do evento.
- **Atenção:** os eventos pendentes vão no próximo `SaveChanges` do escopo. Publique no mesmo handler que grava as alterações do módulo.
- **Pendências:**
  - pré-gerar o código do Wolverine (`TypeLoadMode.Static`) para acelerar a inicialização em produção;
  - painel do Hangfire, que exige autorização (Etapa 3);
  - `StreamMaxLength` do ClamAV alinhado ao limite de upload por plano (Etapa 9).
