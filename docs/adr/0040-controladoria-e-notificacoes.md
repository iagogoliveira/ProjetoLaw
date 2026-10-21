# ADR 0040 — Controladoria e notificações

- **Status:** Proposta (para revisão no PR #39)
- **Data:** 2026-10-21
- **Etapa:** 7D (RF-05 e RF-26)

## Contexto

O plano 7A–7G aprovado inclui consultas da controladoria, indicadores, preferências individuais, regras obrigatórias do escritório e resumo diário às 07:30 em America/Sao_Paulo. O 7C está na main pelo PR #38. As telas da controladoria permanecem no 7F.

## Decisões confirmadas

- Processo sem andamento: somente processos ativos; último andamento processual registrado, ou data de cadastro quando não houver andamento. Limite padrão de 30 dias ajustável na consulta. Confirmado pelo usuário em 2026-10-21.
- Em 2026-10-21, o usuário aprovou: tempo médio entre recebimento e triagem; prazos cadastrados após a data interna; responsável inativo como pendência; sem evidência inclui prazos abertos vencidos ou cumpridos legados sem protocolo. Retrabalho corresponde a triagem refeita, sem ocorrências enquanto não existir esse fluxo.
- Perda de prazo, alteração de calendário e prazo fatal no dia são obrigatórios no aplicativo, para responsável e coordenação autorizados (o conferente continua incluído nos alertas existentes). Resumo diário às 07:30 opcional por usuário, com agenda do dia, prazos dos próximos sete dias e publicações pendentes. Outros canais permanecem futuros.

## Decisões técnicas

- Cada módulo consulta somente suas tabelas e publica projeções mínimas por Contracts. Platform compõe o relatório; nenhuma leitura de DbContext ou de tabelas de outro módulo.
- O painel usa a permissão schedule.control.read, concedida aos perfis padrão Administrador, Sócio e Controladoria por migration aditiva. A fonte de processos parados usa cases.read, como a consulta de andamentos existente; não exige publications.read. As consultas usam os contratos existentes de acesso a processos e diretório de membros. Contagens e indicadores recebem o mesmo filtro de acesso das linhas, inclusive sigilo. Filtros de consulta não concedem acesso.
- Dapper **2.1.89**, licença **Apache-2.0**, para as consultas da controladoria, conforme o plano aprovado. Licença conferida no [repositório oficial](https://raw.githubusercontent.com/DapperLib/Dapper/main/License.txt), versão e ausência de dependências para net10.0 conferidas no [NuGet](https://www.nuget.org/packages/Dapper/2.1.89). Não utilizar Dapper.Contrib ou outros pacotes adicionais.
- SQL estático e parâmetros separados; sem concatenação de entrada. A conexão é a do DbContext do próprio módulo, aberta pelo EF para aplicar o interceptor de tenant; cada consulta também restringe explicitamente OrganizationId. Transação corrente é preservada. Ausência de tenant falha fechada.
- Jobs usam IClock e o agendador existente com fuso America/Sao_Paulo. Processamento por escritório em escopo próprio; nenhum contexto de usuário ou tenant recebido de entrada externa é aceito como autorização.
- Migrations serão aditivas; tabelas novas de escritório terão RLS, incluindo os registros de preferências, regras e controle de envio usados nesta entrega: notification_preferences, notification_rules e digest_deliveries.


## Comportamento da entrega

- A janela de indicadores é civil em São Paulo, início inclusivo e fim exclusivo, até 366 dias; padrão desde 30 dias antes de hoje até amanhã. As pendências mostram o estado atual, sem limitar a recebimentos da janela. Cada seção retorna total autorizado e até 100 itens (50 por padrão). O total é calculado antes do limite.
- Tempo de triagem usa a triagem concluída dentro da janela e a diferença em minutos para ReceivedAt; sem amostras, valor nulo. Cadastro tardio compara a data civil de ConfirmedAt com InternalDueOn. Prazos abertos com responsável removido também são pendência. Cumpridos sem evidência só podem ser legado/integridade: o domínio continua exigindo protocolo.
- Preferências e regras são entidades próprias auditadas por escritório; não usam o JSON genérico de preferências de tela. Os três alertas críticos formam um piso obrigatório. O escritório pode exigir adicionalmente os tipos existentes de publicação e captura; não pode retirar o piso. O usuário pode salvar suas escolhas, mas o feed calcula a política efetiva antes de paginar e aplica os mesmos filtros aos recibos e ao stream.
- O alerta fatal tem chave por escritório/dia/prazo e referencia o prazo. O feed revalida responsável/coordenação, leitura, escopo e sigilo, e só mostra o lembrete enquanto o prazo está aberto e vence no dia atual. Alertas anteriores de perda e calendário preservam o conferente como destinatário.
- Dispatcher recorrente usa cron 30 7 * * * e o fuso já configurado America/Sao_Paulo. Não executa antes de 07:30 nem envia job de outro dia. O processamento é separado por escritório e por usuário. O opt-in, a conta confirmada, a pertença e as permissões atuais são revalidados antes de exportar.
- E-mail é uma exportação expressamente escolhida pelo usuário, não uma sessão autenticada: não forja MFA nem reutiliza o token de outra pessoa. O feed e as consultas interativas continuam exigindo a autenticação da organização. Cada fonte verifica seu próprio escopo e sigilo; receber e-mail não concede acesso no sistema.
- Resumo contém a agenda atribuída ao destinatário no dia (inclusive audiências), seus prazos abertos no intervalo de sete dias e publicações pendentes que ele pode ler. Pode escolher as seções. Cada seção mostra até vinte itens e seu total; títulos são escapados no HTML. Sem itens visíveis nas seções escolhidas, nenhum e-mail é enviado.
- Advisory locks e índice único por escritório/usuário/dia evitam duplicatas concorrentes e novas execuções após envio registrado. Falha SMTP não marca sucesso e permite retentativa. SMTP não oferece transação distribuída: queda entre aceitação do e-mail e commit pode duplicar a mensagem na retentativa. Não se promete exactly-once nem se perde e-mail marcando sucesso antes do envio.

## Verificação

PostgreSQL real para consultas, RLS e upgrade; permissões, escopos e sigilo nas linhas e totais; relógio controlado e retentativas nos jobs; e-mail via Mailpit; contratos aditivos e SecurityTests. Verificação integral aprovada em 2026-10-21: builds Release/Debug sem avisos/erros, format, 1.161 testes .NET em 20 projetos, gate de 80%, dependências e seis modelos EF; OpenAPI/cliente oficiais com 162 caminhos/287 schemas e oasdiff sem quebra; lint, 134 Vitest, build Angular, audit zero e duas rodadas de 17 Playwright com acessibilidade (uma preparação opcional de carga ignorada em cada). SMTP/Mailpit real, concorrência, retry, autorização e upgrade/RLS verificados. Ambiente encerrado e volumes preservados. As duas migrations do 7D foram aplicadas ao banco local e não devem ser editadas. Evidências no progress. Implementação 676a2c1 publicada no PR #39 em 2026-10-21; aguardando revisão, CI e merge pelo usuário.
