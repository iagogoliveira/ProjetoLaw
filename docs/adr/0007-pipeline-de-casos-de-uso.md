# ADR 0007 — Pipeline de casos de uso próprio, sem mediator

- **Status:** Aceita
- **Data:** 2026-10-16
- **Etapa:** 1

## Contexto

A seção 2.1 pede handlers próprios (`ICommandHandler`, `IQueryHandler`) com decorators de validação, transação e log, sem MediatR. O Wolverine, que entra no PR C para outbox e mensageria, também tem um mediator próprio.

## Decisão

- **Sem mediator.** Endpoints e consumidores injetam o handler diretamente (`ICommandHandler<TCommand, TResponse>`), já decorado. O Wolverine fica restrito a mensageria entre módulos e processos (outbox, inbox, transporte).
- **Dois formatos de handler:** comando e consulta. Comando sem retorno é `ICommand : ICommand<Unit>`, o que deixa 5 decorators em vez de 9.
- **Ordem do pipeline** (seção 4.3), registrada por Scrutor em `AddApplication`:
  - comandos: Logging → Validação → (Autorização de recurso, Etapa 3) → Transação → Handler;
  - consultas: Logging → Validação → Handler.
- **Logging** registra nome, duração e código do erro. Nunca registra o conteúdo do comando, porque ele pode ter CPF ou texto de peças.
- **Validação** usa FluentValidation. As falhas viram `ValidationError`, com o campo em camelCase (o nome do JSON da API) e o `ErrorCode` como código estável.
- **Transação por módulo:** o decorator tira o módulo do namespace do comando (`LexFlow.{Modulo}.Application`) e resolve o `IUnitOfWork` registrado com esse nome (keyed service). Ele confirma em sucesso e desfaz em falha ou exceção. Se o módulo não tiver banco, ou se já houver transação aberta (comando chamado dentro de outro), ele só repassa.
- **Relógio:** `IClock` sobre `TimeProvider`. O `FakeClock` da seção 13.3 é o `TimeProviderClock` sobre o `FakeTimeProvider` (Microsoft.Extensions.TimeProvider.Testing, MIT). `Today` usa America/Sao_Paulo. Os IDs são UUID v7 gerados a partir do relógio.

## Consequências

- Comandos de um módulo só abrem transação no banco desse módulo. Operações entre módulos usam eventos de integração (outbox) e não transação distribuída.
- A regra "handlers `internal sealed`" é verificada por `HandlerConventionTests`.
- Pacotes: FluentValidation 12.2.0 (Apache-2.0) e Scrutor 7.0.0 (MIT), registrados na ADR 0002.
