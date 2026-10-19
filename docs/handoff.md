# Passagem de bastão entre agentes (Claude Code ⇄ Codex)

Este arquivo é a conversa entre os agentes que trabalham no LexFlow. Quem começa uma sessão **lê a última entrada antes de tudo**. Quem termina uma sessão, ou para no meio de um PR, **acrescenta uma entrada no topo**.

Como funciona:
- O estado oficial de cada etapa continua no `docs/progress.md`. Aqui fica o que não cabe lá: o que estava em andamento, o que ficou combinado com o usuário na conversa e o que o próximo agente deve fazer primeiro.
- As regras gerais ficam no `CLAUDE.md` (lido pelo Claude Code) e no `AGENTS.md` (lido pelo Codex). Mudou uma regra ou o "Onde paramos"? Atualize os dois.
- Cada entrada tem: data, agente, branch, o que foi feito, o estado (commitado, com push, PR aberto ou mergeado), o que falta e as decisões novas do usuário.
- Entradas antigas não são apagadas, para o histórico ficar completo.

---

## 2026-10-19 — Codex (6C/DataJud implementado e verificado; commit local)

**Branch/estado:** `codex/etapa-6c-datajud-integracoes`; base `a137919`, arrumação pós-merge enviada em `928f0d3`, política de testes preservada no commit local `05076bb`. Código e documentos do 6C consolidados em commit local desta entrega, sem push/PR novo solicitado. Consultar `git log -1` para o hash. ADR 0034 proposta; não marcar a Etapa 6 concluída.

**Decisões novas do usuário:** “Adotar a sugestão” para importar todo o histórico DataJud na primeira consulta e acrescentar somente novos atos nas seguintes, preservando datas e sem duplicar; “Pode reiniciar quando necessário” para o AppHost local. Reiniciar preservando volumes/cadastros; não rodar AppHost.Tests junto com a instância manual. Nenhuma dessas perguntas continua pendente.

**Feito:** adaptador HTTP DataJud com aliases oficiais, consulta CNJ, `search_after`, desempate validado no endpoint oficial com `match_none`/zero registros, timeout, limite de concorrência e resposta. Configuração por escritório com `settings.integrations.manage`, chave protegida por tenant no Data Protection existente e exclusão de auditoria/resposta. Checkpoints por processo/tribunal, páginas transacionais, hash/índice único e lock por processo, retomada/varredura após cursor inválido e jobs distribuídos em seis horas. Contratos de Litigation para casos judiciais ativos. Migration aditiva com RLS, três endpoints e OpenAPI/cliente regenerados. Manual e guia local revisados e incrementados.

**Verificação completa aprovada:** Release/Debug sem warnings/erros; format aprovado após o último ajuste; **794 testes .NET em 18 projetos**, com cobertura e gate de 80% em todos os módulos (Publications Application 99,1%, Domain 96,3%). Inclui 169 testes da API, 16 de integração de Publications, 11 SecurityTests e 14 AppHost.Tests. Dependências .NET/JavaScript sem vulnerabilidades reportadas; cinco modelos EF sem alterações pendentes; lint, 96 Vitest e build Angular aprovados. OpenAPI gerado pelo teste oficial, somente adições de rotas/schemas, cliente regenerado. Duas rodadas consecutivas de Playwright com `--workers=1`, 14 aprovados cada (1,8/1,4 min), acessibilidade preservada; apenas a preparação opcional de carga ficou fora conforme `LOAD_OUTPUT` ausente. Nenhuma asserção relaxada.

**Logs/ocorrências:** `TestResults/6c-final-validation`, `6c-frontend-*.log`, `6c-playwright-report-1` e `-2`. O erro inicial de escopo no teste do dispatcher foi corrigido. O ajuste do hash para ignorar correções dos rótulos foi compilado antes da integração de Publications; builds/formatação finais repetidos, sem repetir checks não afetados. Perfil HTTP do Aspire exigiu `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true`, como no CI; tentativa inicial preservada em `TestResults/6c-apphost-http-initial.log`. Sem alteração de produção. Manual e guia incrementados, diff/UTF-8 e igualdade dos dois agentes conferidos antes do commit.

**Ambiente mantido:** AppHost em execução para exploração, sessão **`47794`**, log `TestResults/6c-apphost.log`; SPA `:4200`, API `:5238/health/ready` e Mailpit `:8025` responderam HTTP 200. Volumes/cadastros preservados. Perfil HTTP e `LexFlow__PublicPermitsPerMinute=10000` usados nesta verificação local; o limite de produção não mudou. Não encerrar como limpeza nem rodar AppHost.Tests em paralelo. Depois de alterar backend, recompilar Debug antes do próximo `--no-build`.

**Próximos passos:** aguardar pedido de push/PR do 6C e enviar código/documentos no mesmo push; acompanhar CI/revisão, sem fazer merge. Depois do merge pelo usuário, atualizar main, aceitar ADR 0034 com número/data, criar o branch do 6D e fazer a arrumação documental conforme o ciclo. O plano 6A a 6E já está aprovado; não pedir aprovação de novo para escolhas já registradas. Triagem/Scheduling/fechamento são do 6D, telas do 6E. **Fornecedor real de publicações continua pendente de escolha/documentação/acesso**; DataJud não substitui intimações. Não iniciar o próximo PR de implementação antes do merge do 6C.

---

## 2026-10-19 — Codex (testes direcionados durante implementação; gates completos no fechamento)

**Decisão explícita do usuário:** aprovou executar os testes relacionados à mudança durante a implementação e manter a verificação completa no fechamento do PR; pediu registrar no handover e nos dois arquivos de agentes. Alterações feitas no AGENTS.md, CLAUDE.md e neste `docs/handoff.md`, com registro também no progress.

**Regra para Claude e Codex:** durante o desenvolvimento, executar os unitários afetados e, conforme o impacto, integração, segurança e/ou e2e. Ampliar a suíte quando houver falha, alteração transversal ou dúvida de regressão. Não rodar toda a verificação a cada pequeno ajuste. No fechamento do PR de implementação, cumprir a seção Verificação completa sem reduzir nenhum gate: build Release sem warnings, format, todos os testes .NET/cobertura/gate de 80%, SecurityTests, vulnerabilidades/modelos, lint/Vitest/build e Playwright/AppHost quando aplicável. Testes direcionados não autorizam declarar a verificação completa.

**Repetição:** depois de passar, não repetir sem nova mudança, falha ou preocupação concreta. Alterações posteriores exigem repetir os checks afetados e consolidar o estado final. Registrar o que foi executado e seus resultados/limitações no progress; manter os mesmos critérios de acessibilidade e segurança.

**Estado/verificação:** branch `codex/etapa-6c-datajud-integracoes`; arrumação pós-merge do #32 enviada em `928f0d3`, com manual/guia preservados. Este registro é novo commit documental local, sem push solicitado e sem implementação do 6C. AGENTS/CLAUDE sincronizados; UTF-8, conteúdo e diff conferidos antes do commit. Nenhuma mudança de runtime e nenhuma nova suíte completa executada para esta atualização das instruções. Manual e guia revisados, sem impacto nos fluxos do usuário ou na inicialização; não foi necessário alterar seus passos.

**Próximo passo:** seguir a preparação do 6C já registrada, usando esta política de testes. Preservar a instância do AppHost deixada para exploração do usuário e não iniciar AppHost.Tests em paralelo. Implementação nova termina em commit local; push/PR só a pedido. Nunca fazer merge pelo usuário.

---

## 2026-10-19 — Codex (PR #32 mergeado; arrumação no branch do 6C)

**Aviso do usuário:** PR #32 aceito e mergeado. Confirmado no GitHub: `MERGED`, em 2026-10-19, merge `a137919915b0cc0f3eea555500389c7ec4acc08f`; todos os checks aprovados, incluindo AppHost/E2E. Merge feito pelo usuário, não pelo agente.

**Branch/estado:** main atualizada por fast-forward; criado `codex/etapa-6c-datajud-integracoes` dessa base. Os dois commits locais do manual foram preservados por cherry-pick sem conflitos: `2849304` virou `28e8764`; `fd31ce5` virou `9306958`. Não apagar esses documentos nem voltar às instruções anteriores. Esta arrumação documental recebe commit/push no branch do 6C, conforme autorização permanente do ciclo pós-merge; nenhum PR novo solicitado/criado.

**Feito:** 6B marcado como concluído no progress, ADR 0033 aceita (PR #32, 2026-10-19), Onde paramos atualizado e AGENTS/CLAUDE sincronizados. Manual e guia revisados: situação do 6B e base do 6C atualizadas, sem novo fluxo de tela nem comando de inicialização. A regra de revisar ambos após toda melhoria permanece obrigatória para Claude e Codex. Preparação do 6C registrada no progress, seguindo o plano geral já aprovado.

**Verificado:** merge e base conferidos; diff relativo à main somente em documentação; preservação dos arquivos, links/UTF-8 e igualdade dos arquivos de agentes verificadas antes do commit. CI do #32 todo aprovado; mantida a suíte completa registrada do 6B, sem repetir testes de runtime para esta arrumação. SPA `:4200`, prontidão da API `:5238/health/ready` e Mailpit `:8025` responderam HTTP 200 depois da troca de branch. Nenhuma alteração de implementação do 6C nesta entrega.

**Ambiente do usuário:** AppHost mantido em execução na sessão `32406`, log `TestResults/manual-apphost.log`. Não o encerrar como limpeza nem rodar AppHost.Tests em paralelo. Quando nova implementação exigir recompilar Debug/reiniciar o ambiente, considerar que o usuário está explorando as telas e coordenar a interrupção necessária.

**Próximo trabalho — 6C:** estudar contratos/provedores existentes e documentação oficial do DataJud; detalhar o adaptador, consultas de movimentações a cada seis horas distribuídas por processo ativo, cursor/paginação, idempotência, retomada e configurações seguras. O plano 6A a 6E e o início com fake + DataJud já foram aprovados; não pedir novamente aprovação das mesmas decisões. Fornecedor real permanece pendente de escolha, documentação e acesso; DataJud não substitui captura de publicações/intimações. Perguntar apenas dúvidas novas de negócio não cobertas pelas decisões. Triagem/Scheduling/fechamento no 6D; telas no 6E. Implementação nova termina em commit local; push/PR só a pedido, exceto a arrumação pós-merge já autorizada. Nunca fazer merge pelo usuário.

---

## 2026-10-19 — Codex (manutenção obrigatória do manual e guia confirmada)

**Pedido explícito do usuário:** salvar o manual e guia no ambiente local e incrementá-los após cada melhoria, mantendo AGENTS, CLAUDE e o handover atualizados para que o Claude continue corretamente. O handover do projeto é este `docs/handoff.md`; não criar outro arquivo concorrente.

**Estado:** os dois arquivos já foram salvos no repositório no commit local `2849304`: `docs/manual/README.md` e `docs/manual/ambiente-local.md`. Este reforço fica em novo commit documental local, sem push/PR adicional. PR #32 permanece aguardando revisão/merge do usuário; não há autorização de merge.

**Regra para Codex e Claude:** em toda melhoria, revisar **ambos** os documentos; atualizar o manual quanto a telas, ações, permissões e disponibilidade, e o guia quanto a inicialização, dependências, endereços e operação local. Se um deles não for afetado, registrar isso na verificação da entrega em vez de criar instruções artificiais. Atualizar progress e acrescentar entrada neste handoff ao encerrar; AGENTS/CLAUDE permanecem sincronizados. Somente documentar recursos já entregues e marcar claramente os futuros. O manual inicial não tem imagens; podem ser acrescentadas quando os fluxos forem aprofundados.

**Dúvida de arquitetura respondida com código conferido:** o frontend Angular consome contratos HTTP por classes em `data/` e não acessa banco/entidades .NET. As regras definitivas de domínio, permissões e isolamento são aplicadas no backend. Existem validações de formulário e lógica de apresentação no navegador, incluindo a geração da prévia de parcelas; trocar o frontend exige reproduzir essas interações, autenticação e acessibilidade, além de manter o contrato da API. Mudança visual no Angular é localizada; reescrever em outro framework é possível, mas é trabalho maior. Nenhuma reescrita ou mudança de stack foi solicitada/aprovada; a stack continua fixa.

**Verificação:** alterações apenas documentais; conferidos arquivos salvos, links relativos/UTF-8, igualdade AGENTS/CLAUDE e diff antes do commit. Sem alteração de runtime e sem repetir a suíte completa do 6B. O guia de ambiente não teve seus comandos alterados nesta melhoria, pois não houve mudança operacional.

**Ambiente:** AppHost foi deixado para exploração do usuário na sessão `32406` (log `TestResults/manual-apphost.log`), conforme a entrada anterior. Não o encerrar como limpeza nem iniciar AppHost.Tests em paralelo enquanto ele estiver em uso.

**Próximo passo:** continuar documentando cada melhoria com essa regra; aguardar revisão/merge do #32 e pedido para enviar os complementos documentais locais. Depois do merge, seguir o ciclo de arrumação e preparação do 6C, preservando todos os documentos. Nunca fazer merge pelo usuário.

---

## 2026-10-19 — Codex (manual inicial e AppHost disponível para exploração)

**Pedido do usuário:** quer visualizar a aplicação e começar um manual de utilização que evolua com as entregas. Implementado como documentação no repositório; o conector de documentos não está disponível nesta sessão. Não houve pedido de novo push/PR ou merge.

**Branch/estado:** `codex/etapa-6b-captura-alertas`; PR [#32](https://github.com/iagogoliveira/ProjetoLaw/pull/32) aberto, código e documentos do 6B enviados até `1693fd5`. Manual/guia e atualização de continuidade ficam em commit documental local desta entrada, sem push, para não cancelar o CI em execução. ADR 0033 continua proposta. Nenhum código de runtime alterado.

**Feito:** `docs/manual/README.md` cobre primeiro acesso, Mailpit, conta, permissões, configurações, pessoas/conflitos, processos, valores, risco, acordos, audiências, importação e propostas. Inclui roteiro de experimentação e lista de funcionalidades ainda sem tela. `docs/manual/ambiente-local.md` explica iniciar/parar/reabrir pelo PowerShell, endereços, persistência e problemas comuns. Primeira edição sem imagens; próximas entregas devem atualizar os fluxos correspondentes. Regra adicionada ao AGENTS/CLAUDE sincronizados.

**Verificado:** Docker 29.6.2; `dotnet run --project src/Aspire/LexFlow.AppHost --no-build` iniciado com Debug já compilado e sem alterações de backend desde a verificação do 6B. SPA `http://localhost:4200`, API `http://localhost:5238/health/ready` e Mailpit `http://localhost:8025` responderam HTTP 200. Rotas/componentes/traduções/e2e foram consultados para o manual; links, texto e diff documental conferidos antes do commit. Suíte completa não repetida para documentação; resultados do 6B preservados.

**Ambiente em uso:** AppHost permanece executando para o usuário, sessão de execução `32406`, log local `TestResults/manual-apphost.log`. Não encerrar como rotina de limpeza enquanto o usuário explora as telas; não executar AppHost.Tests em paralelo. O usuário cria sua conta pela interface e confirma e-mails pelo Mailpit; nenhuma credencial de demonstração foi criada para ele.

**Próximo passo:** permitir a exploração manual, receber observações e ampliar o manual conforme as telas evoluírem. Enviar o complemento documental somente quando solicitado. Aguardar CI/revisão/merge do #32; depois seguir o ciclo de arrumação e o 6C. Não fazer merge pelo usuário.

---

## 2026-10-19 — Codex (envio e abertura do PR 6B autorizados)

**Branch:** `codex/etapa-6b-captura-alertas`. **Decisão do usuário:** “Crie o PR”. Isso autoriza o push do código/documentação e a abertura do PR para `main`; não autoriza merge. Implementação pronta no commit `6de6b4f`, após `d46b774` (perguntas) e `4bcc2cd` (arrumação pós-merge do #31).

**Estado e envio:** esta entrada registra a autorização antes da publicação, em commit documental para acompanhar o código no mesmo push. Base remota `main` conferida em `f2e01a9`; nenhum PR anterior encontrado para este branch. Após o push, abrir o PR 6B, anexá-lo à conversa e confirmar o endereço/estado no GitHub. A ADR 0033 permanece proposta até o merge pelo usuário.

**Verificado:** diretório limpo antes desta atualização, branch e commits conferidos, documentação de envio sincronizada em progress/AGENTS/CLAUDE e diff documental a verificar antes do commit. Nenhuma mudança de runtime; preservada a verificação completa do `6de6b4f`: 771 testes .NET, cobertura acima de 80%, builds/formatação, pacotes/modelos, lint, 96 Vitest, build Angular e duas rodadas Playwright de 14 testes cada. Ocorrências e relatórios estão registrados na entrada anterior e no progress; não repetir sem nova mudança ou falha.

**Próximos passos:** concluir a publicação autorizada e aguardar CI/revisão/merge pelo usuário. Se houver falha no CI, investigar e corrigir no branch do 6B. Depois do merge, executar o ciclo documental e preparar o 6C; fornecedor real continua pendente, triagem/fechamento são do 6D e telas do 6E. Não iniciar outro PR de implementação antes do merge do 6B.

---

## 2026-10-19 — Codex (6B implementado e verificado; commit local, sem push/PR)

**Branch:** `codex/etapa-6b-captura-alertas`, da main/merge `f2e01a9`. **Estado:** arrumação pós-merge do #31 enviada em `4bcc2cd`; dúvidas registradas no commit local `d46b774`, depois respondidas pelo usuário. Código e documentos do 6B consolidados no commit local desta entrega, sem push nem PR da implementação. ADR 0033 proposta; 6A #31 permanece concluído e ADR 0032 aceita.

**Decisões novas aprovadas:** o usuário respondeu “Adotar a sugestão” às duas perguntas: alertas de captura por e-mail/in-app para membros ativos com `settings.integrations.manage` ou `publications.triage` em escopo de escritório; novas publicações somente in-app/SignalR para `publications.read` com alcance do conteúdo; fontes criadas/reativadas depois de 05h capturam imediatamente, depois de 07h ganham até uma hora; execução concluída com zero publicações conta como sucesso. Nenhuma dessas perguntas continua pendente.

**Feito:** contratos de provedores e fake explícito no AppHost (padrão Disabled fora dele); jobs de captura/monitor separados, janela e retentativas, ativação tardia, timeout, locks PostgreSQL por fonte/dia e hash, deduplicação concorrente, alerta persistido/resolução e métrica. Platform ganhou notificações por referência e recibo por usuário, consumers com inbox e e-mail apenas de alerta. REST e stream SignalR LongPolling renovam pertença/permissões/MFA/escopo/sigilo, restauram avisos persistidos na reconexão e não transmitem texto por broadcast. Migrations aditivas com RLS, quatro rotas OpenAPI novas e cliente gerado.

**Verificado:** Release/Debug sem warnings; format; 771 testes .NET em 18 projetos com cobertura/gate de 80% (Publications.Application 99,2%, Domain 95,9%); pacotes sem vulnerabilidades e cinco contextos sem modelo pendente; lint, 96 Vitest e build Angular. Contrato anterior preservado, só adições. Nove testes API de captura/notificações incluem concorrência e stream SignalR real com reconexão/revogação de alcance. Playwright contra o AppHost aprovado em duas rodadas finais consecutivas, com 14 testes em cada (2,0 min e 1,6 min), relatório HTML e um worker; apenas o preparo opcional de carga foi pulado. AppHost de verificação encerrado ao fim.

**Ocorrências preservadas:** a primeira suíte API pegou o snapshot OpenAPI anterior; após gerar oficialmente o contrato e o cliente, os 164 testes passaram. A primeira regressão Playwright com oito workers, durante verificações .NET, teve cinco falhas em fluxos existentes (controles/esperas, acessibilidade e convite); logs/traces/HTML em `TestResults/6b-playwright-initial-failure*`. A execução inicial de AppHost.Tests foi interrompida para não disputar portas com o AppHost manual; a repetição isolada passou (14 testes). As rodadas finais de navegador usam uma única instância do AppHost e `--workers=1`; não foram relaxadas asserções nem critérios de acessibilidade.

**Próximo passo:** aguardar pedido de push/PR; enviar código e documentos juntos no mesmo push quando autorizado. Progress/AGENTS/CLAUDE atualizados e sincronizados; resultados completos e caminhos dos relatórios no progress. Não repetir testes sem nova mudança, falha ou dúvida que justifique. Push e PR da implementação somente quando o usuário pedir. Depois do merge, seguir o ciclo para o 6C; fornecedor real continua pendente, DataJud não substitui publicações. Triagem/Scheduling/fechamento são do 6D, telas e cliente visual SignalR do 6E. Nunca fazer merge pelo usuário.

---

## 2026-10-19 — Codex (arrumação do #31 enviada; duas regras complementares antes do 6B)

**Branch:** `codex/etapa-6b-captura-alertas`. **Estado:** arrumação pós-merge enviada no commit `4bcc2cd`, a partir de `main`/merge `f2e01a9`. 6A #31 concluído, ADR 0032 aceita. Nenhum código ou PR do 6B criado. Este registro das perguntas fica em commit documental local, para acompanhar o próximo envio autorizado.

**Feito/verificado:** confirmado merge do #31 e main atualizada; branch do 6B criado; progresso/AGENTS/CLAUDE/handoff atualizados, diff documental conferido e AGENTS/CLAUDE idênticos. Lidos novamente RF-05/RF-10, seção da captura e contratos locais de jobs/estrutura. Não repetida a suíte de runtime porque só houve documentação; mantidos os resultados completos do 6A.

**Aguardar respostas do usuário antes de implementar as regras:**
1. Alertas de captura ausente por e-mail/in-app para membros ativos com `settings.integrations.manage` ou `publications.triage` em escopo de escritório? Novas publicações só por in-app/SignalR, para quem tem `publications.read` e alcance do conteúdo, sem e-mail individual? Essa é a proposta, ainda não aprovada especificamente.
2. Fontes criadas/reativadas após 05h capturam imediatamente? Depois de 07h, tolerância de até uma hora desde a ativação antes do alerta? Execução concluída com zero publicações conta como sucesso, ao contrário de falha ou ausência de execução? Essa é a proposta, ainda não aprovada especificamente.

**Motivo:** o plano 6B e os canais foram aprovados, mas os destinatários e essas situações de captura não estavam definidos no plano, especificação ou ADRs. Segue a regra do projeto de perguntar antes de decidir dúvida de negócio; não pedir nova aprovação do plano inteiro. Não presumir resposta pelo silêncio. Com as respostas, registrar decisões, formalizar ADR 0033 proposta e implementar/verificar o 6B; commit local, sem push/PR até pedido. Nunca fazer merge.

---
## 2026-10-19 — Codex (6A #31 mergeado; arrumação e início do 6B aprovado)

**Branch:** `codex/etapa-6b-captura-alertas`, criado da `main` atualizada no merge `f2e01a9` do PR #31. **Estado:** o usuário informou merge e aprovação; confirmado `MERGED` no GitHub em 2026-10-19. Arrumação documental em commit/push neste branch, conforme o ciclo já autorizado. Nenhum PR do 6B aberto.

**Feito:** main atualizada por fast-forward; 6A concluído no progresso; ADR 0032 aceita; AGENTS/CLAUDE sincronizados. Conferidos branch/base, diff documental e ausência de alterações locais anteriores. Mantida a verificação completa do 6A (751 testes .NET, cobertura, frontend e duas rodadas Playwright); esta arrumação não muda runtime.

**Próximo:** seguir a implementação do **6B já aprovado**, um PR por vez: provedores fake, captura idempotente inclusive concorrente, jobs às 05:00 e retentativas até 07:00, monitor independente de captura ausente, alerta persistido sem duplicação/métrica e notificações mínimas in-app/e-mail/SignalR com isolamento e autorização. Detalhar o desenho na ADR 0033 proposta, adicionar testes e executar verificação completa. Não antecipar DataJud/fornecedor real, triagem/fechamento ou telas de fila. A implementação do 6B termina em commit local; push/PR só quando solicitado. Nunca fazer merge.

**Decisões:** nenhuma decisão de negócio nova. Todas as escolhas da aprovação da Etapa 6 permanecem válidas, incluindo fake + DataJud inicialmente e fornecedor real como pendência, núcleo mínimo de Scheduling no 6D, `publications.close` e fechamento por recebimento no fuso legal.

---
## 2026-10-19 — Codex (envio e PR do 6A autorizados)

**Branch:** `codex/etapa-6-publicacoes-andamentos`, base `main`. **Implementação:** commit `a4c2d75` — “Etapa 6A: base de publicações, fontes e andamentos”. O usuário pediu “Faça push e PR”; este registro documental acompanha o código no mesmo push. A entrega segue para revisão como PR 6A, com a ADR 0032 ainda proposta.

**Feito nesta entrega:** conferência do branch, diretório limpo, commit e remoto; verificado que não havia PR aberto para o branch; registro da autorização e preparação do envio e do PR. O resultado da publicação e o link do PR são confirmados na resposta desta conversa. Não há alteração de código em relação à verificação completa anterior: 751 testes .NET, gate de cobertura, frontend e duas rodadas Playwright aprovados. Somente documentação de estado foi atualizada; diff e sincronização AGENTS/CLAUDE conferidos.

**Próximo:** acompanhar o CI e a revisão do PR 6A; aguardar o usuário fazer e avisar do merge. Depois, atualizar a main, aceitar a ADR 0032 com número/data do PR, atualizar progresso/AGENTS/CLAUDE/handoff, criar o branch do 6B e fazer a arrumação pós-merge. Nunca fazer merge pelo usuário. As decisões da Etapa 6 continuam aprovadas; fornecedor real permanece pendente e não bloqueia o 6A.

---
## 2026-10-19 — Codex (plano da Etapa 6 aprovado; PR 6A implementado e verificado localmente)

**Branch:** `codex/etapa-6-publicacoes-andamentos`. **Estado:** último commit enviado `beee338` (arrumação pós-merge do #30); 6A implementado e verificado, em commit local desta entrega, sem novo push e sem PR aberto. O usuário aprovou o plano e as recomendações com “Ok, gostei das sugestões, pode seguir.” Não pedir novamente aprovação das mesmas escolhas.

**Decisões aprovadas:** iniciar com fake + DataJud, mantendo fornecedor real de publicações como pendência explícita; antecipar o núcleo mínimo de Scheduling com confirmação humana e transação local PostgreSQL compartilhada (ADR específica no 6D); fontes/credenciais com `settings.integrations.manage`, consulta com `publications.read`, triagem com `publications.triage`, distribuição com `publications.distribute` e fechamento com nova `publications.close`; escritório individual pode acumular permissões sem segunda aprovação. Fechamento pela data de recebimento em America/Sao_Paulo, incluindo pendências anteriores, e captura esperada ausente impede conclusão integral. A data da publicação continua separada.

**Escopo do 6A:** cinco projetos Publications, fontes OAB/nome/CNPJ, estrutura de captura/publicação/triagem/andamento, RLS, consultas paginadas, entrada interna de publicação com extração/validação do CNJ, vínculo por Contracts, andamento manual com autorização do processo e contribuição na timeline. ADR 0032 proposta. Nenhum provedor externo, job de captura, triagem, fechamento ou tela de fila antecipado.

**Segurança:** o novo `ICaseDirectory` usa o `CaseAccess` do Litigation com a permissão do caso de uso; não há acesso a outro DbContext. A fila sem vínculo exige escopo de escritório e oculta também publicações que mencionam CNJs inacessíveis, inclusive ambíguas, para preservar o sigilo antes de haver vínculo.

**Verificação concluída:** Docker Desktop rodando; builds Release/Debug sem warnings, format sem alterações; **751 testes .NET em 18 projetos**, incluindo os 9 novos de API e os 14 do AppHost; cobertura >=80% em todas as camadas (Publications.Application 100%, Domain 95,4%); NuGet sem vulneráveis; cinco contextos sem mudança de modelo pendente. OpenAPI gerado pelo teste oficial, diff apenas aditivo, cliente TypeScript regenerado. Lint, 96 Vitest e build Angular aprovados. Duas rodadas finais consecutivas de Playwright: **14 aprovados em cada (37,6 s e 29,2 s)**, com relatório HTML e WCAG; apenas preparo opcional de carga pulado. Artefatos ignorados em `TestResults/6a-*` e `src/Web/lexflow-web/playwright-report`.

**Armadilha confirmada:** a repetição imediata das suítes com limite local padrão (120/min por IP) gerou 429 na confirmação de e-mail; traces preservados em `TestResults/6a-playwright-rate-limit-failure`, rodada interrompida. Reiniciar o AppHost com `LexFlow__PublicPermitsPerMinute=10000` (configuração existente, só E2E) permitiu as duas rodadas finais. Não foi mudado limite de produção nem mascarado erro no teste; orientação incluída em AGENTS/CLAUDE. O AppHost da verificação final ficou em execução.

**Próximo:** aguardar o pedido do usuário para enviar o commit local do 6A e abrir o PR. Não fazer push nem abrir PR sem pedido. Depois do merge do 6A pelo usuário, seguir o ciclo pós-merge e o 6B; não implementar 6B no mesmo PR.

---

## 2026-10-19 — Codex (PR #30 mergeado; arrumação e plano proposto da Etapa 6)

**Branch:** `codex/etapa-6-publicacoes-andamentos`, criado da `main` atualizada no merge `33ec6bb` do PR #30. **Estado:** esta arrumação documental segue em commit/push neste branch, como já autorizado pelo ciclo pós-merge. Nenhum código da Etapa 6 foi implementado e nenhum PR da Etapa 6 foi aberto.

**Feito:** o usuário informou que os testes passaram e que fez o merge; o estado `MERGED` e a data de 2026-10-19 foram confirmados no GitHub. `main` atualizada por fast-forward; 5E2 e Etapa 5 concluídos no progresso; ADR 0031 aceita; `AGENTS.md` e `CLAUDE.md` atualizados e sincronizados. O registro local da leitura integral da especificação/ADRs foi preservado e incluído aqui, sem perda do histórico.

**Verificação desta arrumação:** conferência do merge e da base do branch, diff somente documental, `git diff --check` e igualdade de `AGENTS.md`/`CLAUDE.md`. Não houve alteração de runtime, pacote, migration, endpoint ou contrato; a suíte completa aprovada no 5E2 e o CI informado pelo usuário continuam sendo a última verificação de código.

### Plano da Etapa 6 — proposta para aprovação, sem implementação

**Objetivo:** RF-10 e RF-11, seção 10 da `implementacao.md`: captura rastreável de publicações, vinculação ao processo, triagem da controladoria, andamentos e fechamento diário. Um PR por vez, com a verificação completa e uma ADR por mudança de arquitetura. O plano abaixo ainda não foi aprovado pelo usuário.

| PR | Entrega proposta | Testes previstos |
| --- | --- | --- |
| 6A | Criar os cinco projetos de Publications, entidades `CaptureSource`, `CaptureRun`, `Publication`, `PublicationTriage` e `CaseMovement`, schema/RLS, consultas e gravação manual; fontes de OAB/nome/CNPJ; contratos para localizar processos e validar alcance sem acessar outro DbContext; vínculo por CNJ validado e contribuição na linha do tempo | Domínio, migrations do zero/incrementais, API, descoberta automática e casos entre organizações nos SecurityTests, escopo e muralha ética, OpenAPI/cliente gerado |
| 6B | `IPublicationProvider`/`IMovementProvider` com fake determinístico; captura das 05:00, retentativas até 07:00 e retomada; hash de diário/data/texto normalizado; estados vinculada/não vinculada; monitor independente da captura, alerta persistido sem duplicação e métrica `lexflow_capture_missing_total`; infraestrutura mínima de notificações in-app/e-mail e SignalR com autorização por usuário/escritório/processo | Reexecutar captura não duplica; nenhuma publicação é descartada; provedor fake indisponível gera alerta; sucesso posterior resolve a situação de captura; reconexão e isolamento das notificações |
| 6C | Adaptador DataJud de metadados/movimentações públicas e adaptador do fornecedor contratado (dependente da escolha e do contrato); configurações/credenciais fora do código; andamentos a cada 6 h por processo ativo, distribuídos ao longo do dia; cursor por tribunal, paginação `search_after`, idempotência e reprocessamento | Contratos de provedores com respostas gravadas/anônimas, indisponibilidade, paginação, deduplicação, relógio falso e limites de concorrência; testes reais opcionais sem dependência externa no CI |
| 6D | Triagem com as seis classificações da especificação, distribuição, vínculo manual auditado e criação de prazo/tarefa/audiência; fechamento diário com responsável e bloqueio por pendências. Antecipação mínima de Scheduling e transação entre módulos dependem de aprovação (abaixo) | Rollback conjunto com falha induzida; repetição idempotente; permissão/escopo de cada recurso; fechamento recusado enquanto houver pendência; fechamento auditado |
| 6E | `features/publications`: configuração das fontes, fila com filtros e paginação, não vinculadas, painel texto/formulário, CNJ destacado, atalhos de teclado, distribuição e fechamento; contador no menu, notificações/SignalR e andamentos na linha do tempo | Vitest de cada store, chaves pt-BR/en iguais, Playwright contra o AppHost em duas rodadas, `expectAccessible` nas telas novas e casos de acesso negado |

**Permissões propostas, para validação no plano:** fontes e credenciais com `settings.integrations.manage`; consulta com `publications.read`; triagem com `publications.triage`; distribuição com `publications.distribute`; andamentos manuais com `cases.edit` e alcance do processo. O fechamento pode usar `publications.triage` ou uma permissão própria, conforme a decisão do usuário. Novas permissões exigem catálogo, perfis e testes, inclusive nos escritórios existentes quando aplicável.

**Vínculo e sigilo:** os dados ligados a processo devem implementar `ICaseScoped` e respeitar o alcance completo e o sigilo do processo; `ICaseVisibility` hoje cobre os ids ocultos por sigilo e precisará ser complementado para as consultas necessárias. CNJs ambíguos não devem ser vinculados arbitrariamente; permanecem para conferência manual. Não expor texto nem notificações de processo fora do alcance do usuário.

**Dependência e atomicidade a aprovar:** a especificação exige que a triagem crie prazo/tarefa/audiência na mesma transação, mas Scheduling só está previsto para a Etapa 7. A ADR 0007 e o `IUnitOfWork` atual abrem transação por módulo, portanto apenas gravar um evento no outbox não atende ao critério. Recomendo antecipar o núcleo mínimo de prazos/tarefas (processo, responsável, data fatal explícita e estado inicial, com validação e confirmação humana) e criar um coordenador de transação local compartilhada entre os contextos participantes, com comunicação por Contracts e rollback conjunto. A calculadora de prazos, agenda completa, feriados forenses, revisão e cumprimento com evidência seguem na Etapa 7. O desenho da transação e a exceção à decisão por módulo devem ser formalizados em ADR antes de implementar; não usar gravações sequenciais independentes como se fossem atômicas.

**Critérios de aceite propostos (nomes de testes ainda não implementados):**
- `Capturing_twice_does_not_duplicate_publications`;
- `Every_captured_publication_is_linked_or_waits_for_linking`;
- `An_unavailable_provider_raises_a_missing_capture_alert`;
- `A_failed_triage_rolls_back_the_created_deadline`;
- adicional: `Closing_a_day_with_pending_publications_is_refused` e testes de isolamento/escopo em todas as rotas e notificações.

**Dúvidas para o usuário antes de implementar:**
1. Qual fornecedor de publicações/intimações está contratado e há documentação/sandbox? Se ainda não houver, autoriza começar com fake + DataJud e manter o adaptador contratado como pendência explícita (a etapa não será declarada integralmente concluída sem ele)? DataJud atende movimentações públicas; não substitui a captura contratada de diários/intimações.
2. Aprova antecipar o núcleo mínimo de Scheduling para gerar os registros reais na triagem, com responsável/data informados e confirmados pelo usuário, mantendo a calculadora e a agenda completa na Etapa 7? Aprova a proposta de transação local compartilhada formalizada em ADR?
3. Fontes/integrações ficam com o Administrador (`settings.integrations.manage`) e o fechamento com quem pode triar, ou o fechamento deve ter permissão própria?
4. Para o fechamento, "do dia" significa data de captura/recebimento ou data de publicação no diário? Recomendo capturadas/recebidas até a data escolhida, no fuso legal, incluindo pendências anteriores para não ocultar atraso; confirmar antes de gravar essa regra.

**Fontes técnicas consultadas para o plano:** [API pública do DataJud](https://datajud-wiki.cnj.jus.br/api-publica/), [acesso](https://datajud-wiki.cnj.jus.br/api-publica/acesso/) e [paginação](https://datajud-wiki.cnj.jus.br/api-publica/exemplos/exemplo3/), além da especificação, das ADRs e do código local. A chave pública vigente deve ser configurada, nunca copiada para o código/documentação do projeto.

**Próximo agente:** aguardar respostas e aprovação do plano. Não implementar nem abrir o PR 6A ainda. Após aprovação, registrar as decisões no progresso, detalhar apenas o primeiro PR e executar um PR por vez.

---

## 2026-10-19 — Codex (leitura integral da especificação e das ADRs)

**Branch:** `etapa-5e2-telas-valores-importacao`. **Estado:** PR #30 aberto na sessão anterior, com os commits `e993a8b` (implementação) e `775484d` (autorização do envio). Esta entrada é somente um registro local da leitura, sem alteração de código nem novo push.

**Pedido do usuário:** ler integralmente os dois documentos de `docs/spec` e todos os documentos de `docs/adr`. Antes deste pedido, a especificação havia sido consultada por seções pertinentes ao 5E2; isso foi esclarecido ao usuário.

**Leitura concluída:** `docs/spec/implementacao.md` e `docs/spec/requisitos-v3.md`, do início ao fim, e todas as 31 ADRs existentes, de 0001 a 0031, incluindo contexto, decisões, consequências, pendências e atualizações. As decisões posteriores registradas nas ADRs e no progresso prevalecem sobre os exemplos genéricos da especificação. Esta leitura não é uma nova auditoria do código nem uma nova execução dos testes.

**Próximo:** aguardar o usuário avisar do merge do PR #30 antes da arrumação e do planejamento da Etapa 6. Nenhuma nova decisão de negócio ou implementação nesta solicitação.

---

## 2026-10-19 — Codex (envio do 5E2 aprovado pelo usuário)

**Branch:** `etapa-5e2-telas-valores-importacao`. **Commit de implementação:** `e993a8b` (`Etapa 5E2: telas de valores, acompanhamento e importação`).

**Decisão do usuário:** aprovou o resultado e autorizou enviar o commit e criar o PR para a `main`. Este registro é feito antes do envio para que código e documentação sigam juntos em um único push. Nenhuma mudança de código após a verificação completa registrada abaixo; este commit só documenta a autorização.

**Estado neste registro:** implementação e documentação commitadas localmente; envio e criação do PR autorizados. Consulte o PR pelo branch no GitHub para obter o número, a URL e o estado atual do CI, antes de tentar reenviar ou criar outro PR.

**Próximo:** conferir o CI e aguardar o merge pelo usuário. Nunca fazer merge. Quando o usuário avisar do merge, seguir a arrumação e o planejamento da Etapa 6 descritos na entrada anterior. A Etapa 5 e a ADR 0031 ainda aguardam o merge para serem marcadas como concluída/aceita.

---

## 2026-10-19 — Codex (5E2 implementado e verificado)

**Branch:** `etapa-5e2-telas-valores-importacao`. **Estado:** código e documentos neste commit local; sem push, sem PR e sem merge. O plano aprovado foi implementado diretamente, sem nova decisão de negócio.

**Feito:**
- Abas Pedidos e valores, Risco, Acordos e Audiências, em componentes próprios com carregamento ao abrir, forms reativos, OnPush, SignalStores e permissões. Chamadas concentradas no `CasesApi`.
- Pedidos com correção por data, totais e histórico; risco por processo/pedido e histórico; fluxo completo dos acordos com gerador de parcelas (centavos na última, vencimentos ancorados no primeiro dia escolhido), aprovação, homologação e baixa manual; audiências por formato, pessoas e membros, redesignação com histórico e realização/cancelamento.
- `/processos/importar`: modelo, envio, consulta periódica, prévia, confirmação, andamento, resultado e relatório XLSX como Blob. A URL permite retomar após recarregar e a consulta para ao sair da tela.
- O e2e revelou uma falha anterior no `CaseImportJobs.ImportAsync`: a contagem final consultava o banco antes de salvar os erros da revalidação e zerava as inválidas. Corrigido para contar os erros em memória e os persistidos na retomada. O teste de integração das 10 mil linhas agora também confere os totais finais 10.000 / 9.700 / 300.
- Proteção contra consulta de histórico durante a gravação do pedido, com regressão no Vitest. Datas de audiência usam o fuso legal, inclusive as regras históricas de horário de verão.
- Textos pt-BR/en, testes das cinco stores e dos auxiliares, e2e novos e relatório Playwright ligado também localmente, com trace/screenshot nas falhas.
- ADR 0031 proposta, `progress.md` atualizado e `AGENTS.md`/`CLAUDE.md` sincronizados integralmente (o `CLAUDE.md` anterior tinha só um resumo das regras).

**Verificado:** builds Release e Debug sem warnings; `dotnet format --verify-no-changes`; 721 testes .NET em 16 projetos (incluindo 14 do AppHost), com cobertura e gate de 80% aprovados; Litigation.Application 92,4% e Domain 97,9%; sem pacotes vulneráveis nem mudanças de modelo pendentes nos quatro módulos; lint, build Angular e 96 Vitest aprovados. **Playwright completo contra o AppHost: 14 aprovados em duas execuções consecutivas**, com WCAG 2.1 AA nas abas novas e na importação. Só o preparo da carga é pulado. Nenhum pacote, migration ou contrato novo.

**Armadilhas confirmadas nesta sessão:**
- A CLI do Aspire executou API/Worker em Debug mesmo ao chamar `dotnet run ... -c Release --no-build`. Depois de mudar o backend, compile Debug antes de usar `--no-build` e aguarde HTTP 200 em `:4200` antes dos e2e.
- Para avançar passos consecutivos, além de `.filter({ visible: true }).last()`, espere o conteúdo do passo anterior ficar escondido. O auxiliar `e2e/support/cases.ts` usa o `aria-controls` do cabeçalho expandido para isso.
- Use relatórios de cobertura de uma mesma versão dos fontes para o gate; a rodada final isolada está em `TestResults/5e2-final-validation`. O relatório de telas mais recente está em `src/Web/lexflow-web/playwright-report/index.html`.

**Próximos passos combinados com o usuário:**
1. Aguardar o pedido de push/PR. Código e documentos no mesmo push, pois o CI cancela o run anterior a cada push. Nunca fazer merge.
2. Quando o usuário avisar do merge, atualizar a `main`, concluir o 5E2 e a Etapa 5 no progresso, aceitar a ADR 0031 e criar o branch da Etapa 6; atualizar `AGENTS.md`, `CLAUDE.md` e esta passagem; fazer commit e push da arrumação.
3. Só então planejar a Etapa 6 (publicações e andamentos, seção 10 da `implementacao.md`), apresentar o plano e as dúvidas de negócio, e esperar aprovação antes de implementar. Sem pendência de código ou verificação no 5E2.

---

## 2026-10-19 — Claude Code (plano do 5E2 aprovado; passagem para o Codex)

**Branch:** `etapa-5e2-telas-valores-importacao` (já criado e enviado, com a arrumação do #29). **Nenhum código do 5E2 foi escrito.** O usuário aprovou o plano e pediu para outro agente (Codex) continuar daqui.

**Plano do PR 5E2 (aprovado pelo usuário em 2026-10-19; implementação ainda não começou).** Só frontend: as APIs existem desde o 5C1 ao 5D, e não há permissão nova.
- **Aba "Pedidos e valores"** no detalhe do processo (`features/cases/pages/case-detail-page.*`):
  - lista dos pedidos (objeto da tabela "Objetos/pedidos" via `options.claimTypes`, descrição, forma de correção, valores por instância);
  - cadastrar, editar e excluir (`cases.edit`; rotas `/api/v1/cases/{id}/claims`);
  - campo "Atualizar até" (padrão: hoje) → `GET /cases/{id}/claims?updateTo=`: valores corrigidos, totais por instância e o aviso `correctionError` por pedido;
  - histórico de cada pedido (`GET /cases/{id}/claims/{claimId}/history`).
- **Aba "Risco":**
  - risco atual (`GET /cases/{id}/risk`) com provisão e passivo contingente;
  - formulário de avaliação só com `*lfCan="'cases.risk.assess'"` (`POST /cases/{id}/risk-assessments`), por processo ou por pedido;
  - histórico (`GET /cases/{id}/risk/history`).
- **Aba "Acordos":**
  - lista (`GET /cases/{id}/settlements`) com situação, valor pedido × acordado (`difference`/`differencePercent`: economia quando o cliente paga, deságio quando recebe), rodadas e parcelas (`isOverdue` em destaque);
  - ações conforme a situação: nova rodada (`offers`), enviar termos (`submit`), aprovar (`approve`, só com `cases.settlements.approve`), devolver (`return`), homologar (`homologate`), recusar (`reject`), cancelar (`cancel`), baixa (`installments/{installmentId}/pay`);
  - **gerador de parcelas** na tela: número, primeiro vencimento e intervalo mensal, com a última parcela ajustando os centavos para a soma bater com o valor acordado (o servidor recusa com `litigation.settlement_installments_mismatch`).
- **Aba "Audiências":**
  - lista (`GET /cases/{id}/hearings`) por data, com o histórico de redesignações;
  - agendar e editar: o formato pede local (presencial), link (virtual) ou os dois (híbrida); advogado de `options.members`, preposto e testemunhas (busca de pessoas como no assistente);
  - redesignar com motivo (`reschedule`), realizar com resultado ou ata (`hold`), cancelar (`cancel`).
- **Tela de importação** `/processos/importar` (`cases.create`, link na lista):
  1. modelo (`GET /cases/imports/template`) e envio (`POST /cases/imports`, multipart campo `file`, resposta 202 com `importId`/`jobId`);
  2. acompanhamento por consulta periódica de `GET /cases/imports/{id}` (ou `GET /api/v1/jobs/{jobId}`) até sair de `validating`/`importing`;
  3. pré-visualização com totais, `preview` e os primeiros `errors`;
  4. confirmação (`POST /cases/imports/{id}/confirm`);
  5. andamento e resultado;
  6. relatório das inválidas (`GET /cases/imports/{id}/errors`, XLSX; baixar como Blob, ver `features/funnel/data/save-file.ts` e o spread `{ responseType: 'blob' } as object` usado no `FunnelApi`).
- **Onde pôr o código:**
  - chamadas novas no `CasesApi` (único ponto de HTTP);
  - stores novas em `features/cases/data` (ex.: `claims-store.ts`, `settlements-store.ts`, `hearings-store.ts`, `import-store.ts`), com testes Vitest no padrão de `cases-stores.spec.ts`;
  - se o detalhe crescer demais, extrair cada aba em componente próprio em `features/cases/pages/tabs/`.
- **Textos:** pt-BR e en no `core/i18n/*.json`, com as mesmas chaves. Os erros `litigation.*` e `platform.*` já estão traduzidos.
- **E2E** (`e2e/cases-values-flow.spec.ts` e `e2e/case-import-flow.spec.ts`), contra o AppHost, onde o Worker roda os jobs de verdade:
  - importação de CSV com linhas válidas e inválidas até a gravação e o relatório;
  - acordo da proposta à homologação e baixa de parcela;
  - audiência redesignada e realizada;
  - pedido com valor atualizado e histórico;
  - `expectAccessible` em cada aba;
  - abrir mat-select pelo teclado e clicar no "Próximo" visível com `.last()` (armadilhas no `AGENTS.md`);
  - rode os e2e com o relatório ligado: no 5E1, a primeira execução logo depois de subir o AppHost falhou uma vez num `toBeVisible` sem detalhe capturado.
- **Ao concluir:**
  - ADR 0031;
  - seção do 5E2 no `progress.md`: o 5E2 fecha a Etapa 5, então marque a etapa como concluída depois do merge;
  - `AGENTS.md`, entrada aqui, verificação completa e commit;
  - push e PR só quando o usuário pedir.

**Próximo agente:** leia o `AGENTS.md` (regras e armadilhas) e implemente o 5E2 conforme o plano acima, no branch `etapa-5e2-telas-valores-importacao`. Não é preciso mostrar o plano de novo, porque já foi aprovado; dúvidas de negócio que surgirem vão para o usuário.

---

## 2026-10-19 — Claude Code (arrumação depois do merge do #29)

**Branch:** `etapa-5e2-telas-valores-importacao`, criado a partir da `main`, com este commit de arrumação e push.

**Feito:**
- PR #29 (5E1) mergeado pelo usuário. Antes do merge, duas correções de CI:
  - a exceção do oasdiff;
  - o e2e passou a abrir os mat-select pelo teclado (registrado no `AGENTS.md`).
- `progress.md`: 5E1 concluído. ADR 0030 aceita. `AGENTS.md` atualizado.

**Próximo:** planejar o PR 5E2 (abas de Pedidos e valores, Risco, Acordos e Audiências, e a tela de importação) e mostrar o plano ao usuário antes de implementar.

---

## 2026-10-18 — Claude Code (PR 5E1 implementado)

**Branch:** `etapa-5e-telas-processos` · **Estado:** commitado; o push e o PR aguardam o pedido do usuário.

**Feito:**
- **Backend:**
  - preferências de tela por usuário (`platform.user_preferences`, `GET`/`PUT /api/v1/me/preferences`);
  - opções de cadastro (`GET /api/v1/cases/options`);
  - correção do contrato (`CaseAssignmentResponse`).
- **Frontend `features/cases`:**
  - lista com filtros salvos, colunas e lote;
  - assistente em passos;
  - detalhe com ações e as abas Resumo, Partes, Hierarquia, Linha do tempo e Sigilo;
  - numeração nas configurações;
  - textos pt-BR e en.
- **E2E** `cases-flow.spec.ts`: passou contra o AppHost, junto com a suíte inteira (12 aprovados).
- ADR 0030, `progress.md` e `AGENTS.md` atualizados.

**Atenção no CI:** o oasdiff vai acusar a troca do esquema de `CaseResponse.assignments`. É uma correção de descrição, porque o JSON não mudou. O CI agora lê `docs/openapi/oasdiff-ignore.txt`: copie para lá a mensagem exata que o CI mostrar e faça um commit pequeno.

**Próximo agente:**
1. Quando o usuário pedir, faça o push e abra o PR do 5E1. Trate a exceção do oasdiff como descrito acima.
2. Depois do merge, faça a arrumação:
   - ADR 0030 aceita;
   - crie o branch do 5E2;
   - atualize o `AGENTS.md` e escreva uma entrada aqui.
3. Planeje o 5E2.

---

## 2026-10-18 — Claude Code (arrumação depois do merge do #28)

**Branch:** `etapa-5e-telas-processos`, criado a partir da `main`, com este commit de arrumação e push.

**Feito:**
- PR #28 (5D) mergeado pelo usuário. Antes do merge, uma correção de CI: `Path.GetFileName` no Linux (registrado no `AGENTS.md`).
- `progress.md`: 5D concluído, e os 3 critérios da Etapa 5 atendidos.
- ADR 0029 marcada como aceita.
- "Onde paramos" do `AGENTS.md` atualizado.

**Pendência anotada:** o upload dos modelos de proposta (Crm) também usa `Path.GetFileName`. Um nome com caminho do Windows ficaria inteiro num servidor Linux. Isso pede um PR pequeno, se o usuário quiser.

**Plano do 5E mostrado ao usuário** (aguardando aprovação), dividido em dois PRs:
- **5E1:** `features/cases` com:
  - lista com filtros salvos, colunas configuráveis e operações em lote;
  - assistente de cadastro em passos;
  - detalhe com Resumo, Partes, Hierarquia, Linha do tempo, Sigilo e as abas reservadas (Documentos, Prazos, Financeiro);
  - aba "Numeração de processos" nas configurações;
  - e2e.
- **5E2:** abas de Pedidos e valores, Risco, Acordos e Audiências, e a tela de importação, com e2e contra o AppHost.

**Decisões do usuário (2026-10-18):**
- Filtros salvos e colunas da lista ficam **no servidor, por usuário** (preferências por tela, no backend do 5E1).
- O 5E é dividido em **5E1 e 5E2**.

---

## 2026-10-18 — Claude Code (PR 5D implementado)

**Branch:** `etapa-5d-timeline-importacao` · **Estado:** PR aberto (`gh pr list --head etapa-5d-timeline-importacao`), aguardando o CI e o merge do usuário.

**Feito:**
- **Linha do tempo:** `GET /cases/{id}/timeline`, com o contrato `ITimelineContributor` e a contribuição do Litigation.
- **Histórico de situação:** `case_status_changes`, só com inserções.
- **Lote:** `POST /cases/batch`.
- **Importação:** envio, validação em job, pré-visualização, confirmação, gravação retomável, relatório e modelo. O critério 2 está atendido.
- **Contratos novos:** `IJobTracker`, `ILookupItems`, `IOrganizationDirectory.ListAsync`/`MemberUserIdsAsync` e `IPeopleRegistry`.
- **Pacote:** ClosedXML 0.105.1, com a SixLabors.Fonts presa na 1.0.1. Registrado nas ADRs 0029 e 0002.
- **Testes de API:** `RecordingJobScheduler` e `RunJobsAsync` na fábrica, para executar os jobs.

**Próximo agente:**
1. Confirme com o usuário se o PR do 5D foi mergeado (`gh pr list --head etapa-5d-timeline-importacao --state all`). Não faça o merge você mesmo.
2. Depois do merge, faça a arrumação:
   - `progress.md` e ADR 0029 como aceita;
   - crie o branch `etapa-5e-telas-processos`;
   - atualize o `AGENTS.md` e escreva uma entrada aqui;
   - faça commit e push.
3. Planeje o 5E (frontend e e2e dos processos).

---

## 2026-10-18 — Claude Code (arrumação depois do merge do #27)

**Branch:** `etapa-5d-timeline-importacao`, criado a partir da `main` (3a87168), com este commit de arrumação e push.

**Feito:**
- PR #27 (5C2) mergeado pelo usuário;
- `progress.md`: 5C2 marcado como concluído e a linha de status atualizada;
- ADR 0028 marcada como aceita;
- "Onde paramos" do `AGENTS.md` atualizado.

**Combinado com o usuário:**
- Docs e código no mesmo push sempre que der: o CI cancela o run anterior a cada push novo no PR (`cancel-in-progress`). O "X" de cancelado não é falha de teste.

**Plano do 5D mostrado ao usuário** (aguardando aprovação):
- linha do tempo, com `ITimelineContributor` e `case_status_changes`;
- importação XLSX/CSV: upload, validação em job, pré-visualização, confirmação, gravação em lotes e relatório XLSX;
- operações em lote: responsável, fase e situação, em até 1.000 processos;
- ClosedXML 0.105.1 com a SixLabors.Fonts fixada em 1.0.1 (Apache-2.0); a 2.x tem licença comercial;
- ADR 0029.

**Decisões do usuário sobre a importação (2026-10-18):**
- Cliente ou parte contrária fora do cadastro: cadastrar automaticamente. Se o CPF/CNPJ já existe, usar a pessoa existente.
- Pasta interna: coluna opcional que mantém a numeração legada (única no escritório); vazia, usa a numeração do escritório.
- CNJ que já existe no escritório: a linha vai para o relatório (`litigation.cnj_taken`) e o processo existente não muda.
- Linhas inválidas: importa as válidas depois da confirmação e entrega o relatório das inválidas.

---

## 2026-10-18 — Claude Code (PR 5C2 implementado)

**Branch:** `etapa-5c2-risco-acordos` · **PR:** [#27](https://github.com/iagogoliveira/ProjetoLaw/pull/27), aberto e aguardando o CI e o merge do usuário.

**Feito:**
- **Risco:** `RiskAssessment`, só com inserções, e as rotas `GET /cases/{id}/risk`, `GET /cases/{id}/risk/history` e `POST /cases/{id}/risk-assessments`. Permissão nova `cases.risk.assess`, com migration de dados no Organization para os perfis Administrador e Sócio.
- **Acordos:** `Settlement`, com rodadas, envio dos termos, aprovação, devolução, homologação, recusa, cancelamento e baixa de parcela, mais o indicador pedido × acordado. A homologação publica `SettlementHomologatedIntegrationEvent`.
- **Audiências:** `Hearing`, com agendar, editar, redesignar (com histórico), realizar e cancelar.
- RLS nas 6 tabelas; risco e redesignações sem `UPDATE` nem `DELETE`. Também `CrossTenantCases`, OpenAPI só com adições, o cliente TypeScript, a ADR 0028 e o `progress.md`.
- **Refatoração:** `CaseWrites` (`Litigation.Application/Cases`) concentra as conferências de "processo aberto" usadas por pedidos, risco, acordos e audiências.

**Próximo agente:**
1. Confirme com o usuário se o PR #27 foi mergeado (`gh pr view 27 --json state`). Não faça o merge você mesmo.
2. Depois do merge, faça a arrumação:
   - `progress.md` e ADR 0028 como aceita;
   - crie o branch `etapa-5d-timeline-importacao`;
   - atualize o "Onde paramos" do `AGENTS.md` e escreva uma entrada aqui;
   - faça commit e push.
3. Planeje o **5D** (linha do tempo, importação XLSX/CSV com ClosedXML e operações em lote). O ClosedXML é pacote novo: confira a licença (MIT) e registre uma ADR.

---

## 2026-10-18 — Claude Code (PR 5C2 em andamento)

**Branch:** `etapa-5c2-risco-acordos`. O plano foi aprovado pelo usuário; a implementação está em andamento e **ainda não tem commit**. Se você (Codex) encontrar arquivos novos sem commit neste branch, são deste trabalho: confira com `git status` e continue de onde parou.

**Plano aprovado:**
- **Risco (`RiskAssessment`):**
  - feito para o processo ou para um pedido;
  - guarda a probabilidade (provável, possível ou remoto), o valor de perda estimado, a justificativa, o autor e a data;
  - só recebe inserções, e a última avaliação é a atual;
  - a resposta classifica: provável vira provisão, possível vira passivo contingente, remoto não entra.
- **Acordos:**
  - `Settlement`, com rodadas (`SettlementOffer`: nós ou a outra parte, valor, condições, data);
  - situações: negociação → aguardando aprovação → aprovado → homologado → cumprido, além de recusado ou cancelado;
  - parcelas (`SettlementInstallment`) com baixa manual, cuja soma precisa bater com o valor acordado;
  - indicador de economia: valor pedido × valor acordado;
  - a homologação publica `SettlementHomologatedIntegrationEvent`.
- **Audiências (`Hearing`):**
  - tipo, formato, data e hora, local ou link, advogado, preposto e testemunhas;
  - situação: agendada, realizada, redesignada ou cancelada;
  - resultado ou ata.

**Decisões do usuário (2026-10-18):**
- **Aprovação do acordo:** a negociação é livre. Os termos finais vão para a aprovação interna antes de serem assinados e homologados. Aprova quem tem `cases.settlements.approve`, inclusive o próprio acordo.
- **Risco:** permissão nova, `cases.risk.assess`. Vai para os perfis padrão Administrador e Sócio, também nos escritórios já existentes (migration de dados, como a da `crm.people.anonymize`).
- **Redesignação:** a audiência é a mesma; ela muda de data e guarda as datas anteriores com o motivo.
- **Parcelas:** baixa manual (data e valor pagos); "vencida" é calculada pela data. Na Etapa 10, o financeiro passa a dar a baixa.

---

## 2026-10-18 — Claude Code (arrumação depois do merge do #26)

**Branch:** `etapa-5c2-risco-acordos`, criado a partir da `main` (bb14aec), com este commit de arrumação e push.

**Feito:**
- PR #26 (5C1) mergeado pelo usuário em 2026-10-18;
- `progress.md`: 5C1 marcado como concluído e a linha de status da Etapa 5 atualizada;
- ADR 0027 marcada como aceita;
- "Onde paramos" do `AGENTS.md` atualizado.

**Próximo:**
- Planejar o PR 5C2 (risco, acordos e audiências) e mostrar o plano ao usuário antes de implementar. O escopo está no `AGENTS.md`.
- Se o Claude Code estiver no meio desse planejamento, a próxima entrada aqui dirá.

---

## 2026-10-18 — Claude Code

**Branch:** `etapa-5c-valores-risco` · **PR:** [#26](https://github.com/iagogoliveira/ProjetoLaw/pull/26), aberto e aguardando o CI e o merge do usuário.

**Feito:**
- PR 5C1:
  - índices monetários (`platform.monetary_indexes`) com seed real de 01/2000 a 09/2026 e importação diária;
  - `IMonetaryCorrection`;
  - pedidos do processo, com valores por instância e histórico;
  - ADR 0027;
  - critério 3 da Etapa 5 atendido, com os 10 casos calculados à mão, documentados na ADR 0027.
- Verificação completa feita; os detalhes estão na seção do PR 5C1 no `progress.md`.

**Decisões do usuário nesta sessão (2026-10-18):**
- **SELIC:** não leva juros extras e se acumula por soma simples.
- **Juros:** simples, pro rata dia, sobre o valor corrigido.
- **Deflação:** entra no cálculo, mas o valor nunca fica abaixo do original.
- **Acordos (5C2):** quem tem `cases.settlements.approve` aprova, inclusive o acordo que a própria pessoa cadastrou.
- **5C dividido:** 5C1 (este) e 5C2 (risco, acordos, audiências).
- **Documentação de agentes:** sempre atualizar o `AGENTS.md` e este arquivo ao terminar um trabalho.

**Próximo agente, faça nesta ordem:**
1. Confirme com o usuário se o PR #26 foi mergeado (`gh pr view 26 --json state`). Não faça o merge você mesmo.
2. Depois do merge, faça a arrumação pós-merge:
   - atualize a `main`;
   - crie o branch `etapa-5c2-risco-acordos`;
   - no `progress.md`, marque o 5C1 como concluído e mude a linha de status da etapa;
   - marque a ADR 0027 como aceita;
   - atualize o "Onde paramos" do `AGENTS.md` e escreva uma entrada aqui;
   - faça commit e push.
3. Planeje o **PR 5C2** e mostre o plano ao usuário antes de implementar:
   - **risco:** `RiskAssessment` por processo e por pedido (provável, possível ou remoto, CPC 25), com histórico;
   - **acordos:** `Settlement` (proposta, contraproposta, aprovação, homologação) e `SettlementInstallment`, com o indicador de economia (pedido × acordado). A homologação publica `SettlementHomologated`, para o financeiro da Etapa 10;
   - **audiências:** `Hearing` (presencial, virtual ou híbrida; local ou link; preposto, advogado e testemunhas; resultado ou ata).

**Atenção:**
- A API do BCB (`api.bcb.gov.br`) recusa as requisições desta máquina. Para dados da SELIC e da TR, use o Ipeadata.
- A Calculadora do Cidadão (`www3.bcb.gov.br/CALCIDADAO`) funciona e serve para conferir cálculos.
