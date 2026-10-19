# ADR 0033 — Captura, monitoramento e notificações

- **Status:** Proposta (para revisão no PR 6B)
- **Data:** 2026-10-19
- **Etapa:** 6 (PR 6B)

## Contexto

O plano aprovado dos PRs 6A a 6E exige captura idempotente, monitor independente da disponibilidade do provedor e avisos com o mesmo alcance das publicações. O 6A criou os dados e as consultas. O 6B entrega a infraestrutura com provedor simulado; DataJud e fornecedor real pertencem ao 6C, triagem/fechamento ao 6D e interface ao 6E.

O usuário aprovou em 2026-10-19 os canais/destinatários e as regras de ativação tardia e resultado vazio. As escolhas estão registradas em `docs/progress.md`.

## Decisão

1. `IPublicationProvider` e `IMovementProvider`, em Publications.Contracts, retornam `Result<T>`. O fake determinístico é habilitado explicitamente por `Publications:Provider=Fake`, inclusive no AppHost. O padrão é `Disabled`, que informa indisponibilidade e não inventa captura real. Não há pacote novo, credencial nem integração externa nesta entrega.
2. O Worker registra dispatchers independentes de captura e monitoramento no Hangfire a cada cinco minutos. Em America/Sao_Paulo, a primeira tentativa elegível ocorre a partir das 05h e as falhas podem ser repetidas a cada dez minutos até 07h. Eventos de criação/reativação disparam captura imediata depois de 05h e agendam o monitor no prazo. Ativação depois de 07h ganha uma hora; a tolerância pode atravessar a meia-noite. Uma execução concluída com zero publicações é sucesso. O cron também retoma trabalhos depois de reinício.
3. Cada execução registra fonte, dia, ativação da fonte, início, fim, quantidade e código estável de falha. Reativação ou mudança do critério de busca renova a ativação, impedindo que um sucesso do critério anterior satisfaça a captura atual. Desativação preserva o histórico e interrompe novos trabalhos.
4. Um advisory lock transacional PostgreSQL por escritório/fonte/dia impede duas capturas ou monitoramento simultâneos da mesma fonte. O provedor tem timeout de trinta segundos. O lote é validado antes da entrada; os locks de hash seguem ordem global para evitar deadlocks entre fontes. A entrada adquire lock por escritório/hash antes da consulta e gravação; o índice único continua como garantia final. Publicação e evento no outbox são gravados na transação local de Publications. Não há transação entre módulos neste PR.
5. `CaptureAlert` persiste uma situação por escritório/fonte/dia. Monitor sem execução bem-sucedida registra ou reabre a situação, publica evento e incrementa `lexflow_capture_missing_total` somente quando a situação muda. Sucesso posterior resolve o alerta. O estado persistido é a fonte da verdade; a métrica é um contador operacional, sem rótulos com dados privados. Monitoramento não chama o provedor e segue funcionando durante sua indisponibilidade.
6. Platform é dono de `Notification` e `NotificationReceipt`, com tenant/RLS e marcação de leitura por usuário. Notificações guardam tipo e referência do recurso, sem copiar texto da publicação. Consumidores com inbox e chave única impedem duplicação persistida. O e-mail usa o envio e as retentativas existentes; a entrega é pelo menos uma vez, podendo repetir se o envio terminar e a confirmação transacional falhar.
7. Alertas de captura vão por e-mail e in-app aos membros ativos com `settings.integrations.manage` ou `publications.triage` em escopo de escritório. O envio exige e-mail confirmado e não envia alerta já resolvido ou de fonte inativa. Novas publicações geram somente aviso in-app/SignalR, sem e-mail individual.
8. A visibilidade é recalculada na leitura pelo contrato `INotificationVisibility`. Publications consulta os contratos de Organization e Litigation: membro ativo, MFA exigido, `publications.read`, escopo e sigilo atuais. Publicações sem vínculo exigem alcance de escritório; CNJs de processos inacessíveis também ocultam candidatas ambíguas. Alterações de permissão, membro ou sigilo afetam avisos já persistidos. A leitura é idempotente, não transfere o recibo a outro usuário e retorna 404 fora do alcance.
9. REST oferece lista com cursor e baixa de leitura. Endpoints universais exigem `identity.profile.read`; as consultas de monitoramento verificam adicionalmente a união das permissões e o escopo de escritório. Recursos com id têm casos entre organizações nos SecurityTests. A paginação filtra a visibilidade antes de completar a página, inclusive quando há mais de duzentos registros invisíveis.
10. SignalR usa o framework ASP.NET Core já aprovado, somente LongPolling, em `/api/v1/notifications/hub`. O método de stream `Watch` consulta notificações não lidas a cada dois segundos, com novo escopo de DI, pertença e autorização renovadas; envia snapshot quando os ids mudam. Usa cabeçalho Bearer e escritório já validados, sem token na query string, grupo global nem conteúdo em broadcast. Reconexão restaura avisos persistidos; REST/cursor permite continuar além dos cinquenta itens do snapshot. O transporte encerra na expiração da autenticação. Backplane e cliente visual ficam para evolução posterior.
11. `TenantOverride` ganha entradas explícitas para jobs internos e conexões com pertença validada. O escritório vem do diretório interno, dos metadados do job ou da seleção autenticada validada; nunca da resposta do provedor. Os módulos continuam sem acesso ao DbContext alheio. A infraestrutura de notificações é registrada somente nos hosts que também registram os diretórios de autorização, preservando o MigrationService.

## Consequências

- O fake permite verificar o fluxo completo sem depender de contratação ou disponibilidade de fornecedor. Não substitui captura real em produção.
- Locks são locais ao PostgreSQL e os jobs podem ser retomados com segurança. A transação permanece aberta durante a chamada limitada ao provedor; a latência e o volume devem ser observados na integração real.
- Notificações por referência reduzem a cópia de dados sensíveis e respeitam revogação de acesso. A consulta periódica do stream é uma solução inicial com custo proporcional às conexões; otimização e escala devem preservar a revalidação.
- A interface de fontes/fila e o cliente visual do stream permanecem no 6E. Captura de movimentações a cada seis horas e adaptadores externos são do 6C. O núcleo de Scheduling e a transação compartilhada da triagem continuam no 6D.

## Verificação

Testes de domínio e horários com relógio controlado; integração da captura concorrente, deduplicação, retorno vazio, falha/recuperação, ativação e destinatários; paginação e leitura por usuário; stream SignalR real com reconexão e revogação de alcance; migrations/RLS, SecurityTests e contrato OpenAPI aditivo. Os resultados da execução completa ficam em `docs/progress.md`.
