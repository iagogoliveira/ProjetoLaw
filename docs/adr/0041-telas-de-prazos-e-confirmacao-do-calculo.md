# ADR 0041 — Telas de prazos e confirmação do cálculo

- **Status:** Proposta (para revisão no PR 7E)
- **Data:** 2026-10-21
- **Etapa:** 7E (RF-23)

## Contexto

O 7A e o 7B entregaram calculadora e ciclo do prazo. O plano 7A–7G aprovado prevê painel com semáforo, calculadora com memória, cadastro, conferência e cumprimento, além de usar a sugestão na triagem. A agenda unificada exige `schedule.events.read` e reúne outros tipos de atividade; usá-la como única consulta do painel impediria a leitura por quem só tem acesso aos prazos.

## Decisão

1. Adicionar `GET /api/v1/deadlines`, com `schedule.deadlines.read`, janela civil pelo vencimento fatal (`from` inclusivo, `until` exclusivo), responsável, equipe, processo e inclusão explícita de encerrados. Limitar a janela a 366 dias e as páginas a 100 itens. Ordenar por vencimento e ID, com cursor. A consulta aplica a interseção de acesso aos prazos e leitura dos processos, incluindo sigilo, antes da paginação. O filtro de processo nunca amplia o conjunto autorizado. `today` é obtido do `IClock` em São Paulo.
2. Em `/prazos`, o semáforo dos abertos segue a decisão do usuário em 2026-10-21: vermelho para fatal vencido ou hoje; amarelo para meta interna vencida/hoje ou fatal nos próximos dois dias corridos; verde para os demais. Cumprido e perdido mantêm identificação própria. Texto e legenda acompanham as cores. Trata-se de urgência de exibição, sem mudar estado ou datas.
3. Cadastro e triagem reutilizam uma calculadora que apenas consulta. Exibir datas, local utilizado e memória completa; aplicar uma sugestão é ação explícita. Editar entradas ou trocar processo invalida a sugestão e a confirmação. O cadastro manual exige nova confirmação humana e o servidor recalcula, mantendo o conflito `scheduling.calculation_changed` quando as datas mudarem.
4. Tribunal e comarca vêm do processo selecionado; UF e município podem ser ajustados na consulta. A tela não pede identificadores internos de tribunal. O cadastro preserva o local retornado na sugestão aplicada. Datas civis são apresentadas sem conversão pelo fuso do navegador.
5. O detalhe usa as operações existentes. Iniciar continua permitido antes da conferência obrigatória; cumprir exige protocolo textual e a revisão quando exigida. Perder exige justificativa. Conferente diferente do autor, salvo escritório com um membro ativo, continua sendo regra do servidor. Alteração de calendário apresenta aviso, sem alterar o prazo confirmado.
6. A aba Prazos do processo deixa de ser um espaço reservado e oferece consulta e navegação ao painel/cadastro/detalhe. As ações são filtradas pelas permissões; o servidor permanece responsável pela autorização de cada operação.
7. Descartar respostas antigas ao mudar filtros, processo ou cálculo. Uma gravação concluída depois da saída da tela não deve provocar navegação. Falha de paginação reinicia a consulta explicitamente, sem continuar de um cursor que omita páginas anteriores.
8. Páginas consomem SignalStores e formulários reativos tipados. Filtros aplicados ficam na URL; até dez conjuntos podem ser salvos explicitamente na preferência pessoal `deadlines.list`. Alterações só são permitidas após ler as preferências existentes ou confirmar sua ausência pelo código `platform.preference_not_found`; falha de leitura ou gravação exige nova leitura antes de editar. Versões impedem uma resposta antiga de sobrescrever o estado salvo. O semáforo usa tokens CSS globais para os temas claro e escuro.

## Consequências e limites

- Nenhum pacote ou migration nova. OpenAPI e cliente são gerados pelos comandos oficiais, com alteração aditiva.
- Agenda, tarefas, painel de controladoria e preferências ficam no 7F; sincronização no 7G.
- A revisão jurídica dos casos e do calendário do 7A permanece pendente; esta interface não encerra esse critério.
- Protocolo textual continua sendo a evidência até a integração com o GED na Etapa 9.

## Verificação

Verificação integral aprovada em 2026-10-21: 1.165 testes .NET em 20 projetos, gate de cobertura de 80%, builds e formatação; dependências e seis modelos EF aprovados; OpenAPI aditivo sem quebra. Frontend com lint, 153 Vitest, build Angular e audit sem vulnerabilidades. Duas rodadas consecutivas de 18 Playwright aprovados contra AppHost (uma preparação opcional de carga ignorada por rodada), incluindo cadastro/conferência/cumprimento, triagem → prazo → cumprimento com evidência → fechamento e WCAG 2.1 AA. Resultados e limites detalhados no progress.
