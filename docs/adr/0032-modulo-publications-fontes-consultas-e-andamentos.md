# ADR 0032 — Módulo Publications: fontes, consultas e andamentos

- **Status:** Aceita (PR #31 mergeado em 2026-10-19)
- **Data:** 2026-10-19
- **Etapa:** 6 (PR 6A)

## Contexto

RF-10 e RF-11 e a Etapa 6 exigem captura rastreável de publicações, vínculo com processos, triagem e andamentos. O plano 6A a 6E foi aprovado pelo usuário em 2026-10-19. Esta primeira entrega cria o módulo e as consultas; a captura agendada entra no 6B, os provedores externos no 6C, a triagem/fechamento no 6D e as telas no 6E.

## Decisão

1. Cinco projetos (Domain, Contracts, Application, Infrastructure e Endpoints), schema próprio `publications`, DbContext e migrations próprios. API, Worker, MigrationService e fixtures registram o módulo. Todas as tabelas de escritório têm tenant e RLS; publicações, triagens e andamentos implementam `ICaseScoped`. A tabela de triagem é apenas estrutura nesta entrega, sem fluxo de baixa antecipado.
2. Fontes monitoradas de OAB, nome e CNPJ têm rótulo, valor normalizado e situação ativa/inativa. OAB e CNPJ reaproveitam os value objects existentes. Cadastro, consulta e edição exigem `settings.integrations.manage`. Desativar preserva o histórico.
3. A publicação mantém diário, datas de disponibilização/publicação, texto original, instante de recebimento em UTC, CNJs candidatos e hash SHA-256 de diário/data de publicação/texto normalizados. O texto integral não é copiado para os changes da auditoria. O índice único `organization_id/hash` separa escritórios e impede duplicações persistidas. O comando interno de entrada não tem endpoint; jobs do sistema o executarão no tenant da fonte. A coordenação das tentativas concorrentes pertence ao 6B.
4. CNJ é extraído com regex limitada por timeout e validado pelo value object do SharedKernel. Apenas um número válido distinto permite vínculo automático; ausente, inválido, não encontrado ou ambíguo permanece sem vínculo. A busca nunca usa o DbContext de outro módulo: novo `ICaseDirectory` em Litigation.Contracts implementa localização no tenant e autorização pelo `CaseAccess` existente.
5. Consultas usam `publications.read`, incluindo seu escopo de dados e a muralha ética. A fila sem vínculo requer alcance de escritório, pois ainda não tem responsável/área/filial de processo. Publicações que mencionam um CNJ fora do alcance também ficam ocultas, mesmo quando ambíguas ou sem vínculo; isso evita revelar conteúdo sigiloso pela fila de não vinculadas. O detalhe usa autorizador de recurso e retorna 404 fora do alcance; negativas de processo sigiloso seguem a auditoria existente.
6. Andamento manual é criado com `cases.edit` e consultado com `cases.read`, sempre autorizando o processo por Contracts. Guarda origem, data do acontecimento e data da coleta, ambas UTC. Consultas de publicações e andamentos usam cursor e limite de 1 a 200.
7. `ITimelineContributor` integra recebimentos e andamentos à linha do tempo já autorizada do processo. Datas empatadas usam ids estáveis para não perder ou repetir acontecimentos. Textos novos têm traduções pt-BR/en.
8. Não há pacote novo. As referências usam as versões e licenças já aprovadas no repositório.

## Decisões aprovadas para as próximas entregas

- Começar com provedor simulado e DataJud. Fornecedor real de publicações continua pendência explícita; a Etapa 6 não será declarada integralmente concluída sem integração validada.
- Antecipar somente o núcleo mínimo de Scheduling, com responsável/datas confirmados pelo usuário; calculadora, feriados e agenda completa continuam na Etapa 7.
- A criação do destino e a triagem devem compartilhar uma transação local PostgreSQL, comunicando-se por Contracts. O desenho e a exceção à ADR 0007 serão detalhados na ADR do 6D, antes da implementação, com teste de rollback conjunto. Esta entrega não altera o pipeline transacional.
- Fechamento com permissão própria `publications.close`, a adicionar ao catálogo/perfis e escritórios existentes no 6D. Escritório individual pode acumular permissões, sem segunda aprovação.
- Fechamento pela data de recebimento em America/Sao_Paulo, incluindo pendências anteriores. Captura esperada ausente impede conclusão integral, distinguindo captura completa de triagem concluída.

## Consequências

- Litigation continua dono das regras de alcance e sigilo. Publications não compartilha entidades nem acessa seu DbContext.
- As consultas de alcance retornam identificadores/CNJs pelo contrato; o volume dessas listas deve ser observado ao ampliar a captura. Não afeta a consulta paginada de processos já existente.
- As fontes podem ser cadastradas e os andamentos registrados antes da integração externa; isso não representa captura real em produção.
- Não há telas de configuração/fila, agenda nem baixa de triagem neste PR.

## Verificação

Testes de domínio, API, migrations do zero/reversão/reaplicação, modelo sem mudanças pendentes, autenticação/permissões, isolamento entre organizações, RLS, escopo/sigilo e paginação da linha do tempo. A execução completa e os números finais ficam em `docs/progress.md`.
