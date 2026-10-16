# ADR 0010 — Trilha de auditoria append-only com encadeamento no banco

- **Status:** Aceita
- **Data:** 2026-10-16
- **Etapa:** 1

## Contexto

A seção 6.4 pede `audit.audit_logs` gravado pelo `AuditSaveChangesInterceptor`, com antes e depois dos campos não sensíveis. A tabela deve ser append-only, e cada linha guarda o hash SHA-256 da anterior por organização. O RF-45 exige uma trilha imutável.

## Decisão

- **Mesma transação.** A entidade de gravação `AuditLogEntry` é mapeada em todo DbContext de módulo. A tabela só é criada pela migration do Platform; nos demais módulos ela fica excluída das migrations. O interceptor, que roda por último, adiciona as linhas ao mesmo `SaveChanges`, então dado e auditoria são gravados no mesmo commit.
- **O que entra em `changes`:**
  - entram os campos do insert (`new`), do update (`old` e `new`, só os alterados) e do delete (`old`);
  - ficam fora os campos `[Sensitive]` e `[NotAudited]`, o token `xmin` e as colunas de controle (`created_*`, `updated_*`), que já estão na própria linha da trilha;
  - entidades com `[NotAudited]` na classe (jobs, chaves de idempotência) ficam fora da trilha;
  - a exclusão lógica é registrada como `Deleted`.
- **Encadeamento no banco.** O trigger `BEFORE INSERT` pega um `pg_advisory_xact_lock` pela organização, lê a última linha e calcula `chain_position`, `previous_hash` e `hash = sha256(hash_anterior | conteúdo)` com `pgcrypto`. Assim a cadeia não depende da aplicação nem da ordem de commit entre processos.
- **Append-only.** Triggers rejeitam `UPDATE`, `DELETE` e `TRUNCATE`. O `REVOKE` por papel do banco (papel da aplicação sem `UPDATE`/`DELETE`) entra na Etapa 3, junto com os papéis do RLS.
- **Verificação.** A função `audit.verify_chain(org)` devolve a primeira posição adulterada, ou `NULL` se a cadeia estiver íntegra.

## Consequências

- Gravações de auditoria da mesma organização são serializadas pelo advisory lock. É aceitável no volume previsto. Se virar gargalo, a seção 6.6 já prevê particionamento na edição Corporativa.
- Os testes não podem limpar `audit_logs`: usam organizações novas a cada teste, ou um banco próprio.
- Evidência: `AuditTrailTests` (antes e depois, campos sensíveis) e `AuditLogTests` (migration real: cadeia sob concorrência, append-only, adulteração detectada).
