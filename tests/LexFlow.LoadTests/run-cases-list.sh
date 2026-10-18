#!/usr/bin/env bash
# Teste de carga da lista de processos (Etapa 5, critério 1) contra o AppHost local
# (`dotnet run --project src/Aspire/LexFlow.AppHost`):
#   1. prepare-load.spec.ts cria, pelo fluxo real, a conta e o escritório da carga e grava o token e os ids;
#   2. seed-cases.sql insere os processos no Postgres do AppHost (docker exec, como dono do banco);
#   3. o k6 (imagem oficial grafana/k6) mede a lista com filtro por área e responsável.
# Variáveis opcionais: TOTAL (padrão 100000), VUS (10), DURATION (60s), E2E_BASE_URL, E2E_MAILPIT_URL.
set -euo pipefail

root="$(cd "$(dirname "$0")/../.." && pwd)"
here="$root/tests/LexFlow.LoadTests"
output="$(mktemp)"
trap 'rm -f "$output"' EXIT

(cd "$root/src/Web/lexflow-web" && LOAD_OUTPUT="$output" \
  E2E_BASE_URL="${E2E_BASE_URL:-http://localhost:4200}" E2E_MAILPIT_URL="${E2E_MAILPIT_URL:-http://localhost:8025}" \
  npx playwright test e2e/load/prepare-load.spec.ts --reporter=line)

value() { node -e "process.stdout.write(String(JSON.parse(require('fs').readFileSync(process.argv[1], 'utf8'))[process.argv[2]]))" "$output" "$1"; }

postgres="$(docker ps --filter ancestor=pgvector/pgvector:pg17-trixie --format '{{.Names}}' | head -1)"
# A senha do dono vem do próprio container (gerada pelo AppHost), sem passar por arquivo.
password="$(docker exec "$postgres" printenv POSTGRES_PASSWORD)"
docker exec -i -e PGPASSWORD="$password" "$postgres" psql -U postgres -d lexflow -v ON_ERROR_STOP=1 \
  -v org="$(value organizationId)" -v user_id="$(value userId)" -v area="$(value areaId)" \
  -v other_area="$(value otherAreaId)" -v client="$(value clientId)" -v total="${TOTAL:-100000}" \
  -f - < "$here/seed-cases.sql"

# Dentro do container, o localhost da máquina é host.docker.internal.
api="$(value apiUrl)"
docker run --rm -i \
  -e API_URL="${api/localhost/host.docker.internal}" -e API_HOST="${api#*://}" -e TOKEN="$(value token)" -e ORG_ID="$(value organizationId)" \
  -e AREA_ID="$(value areaId)" -e USER_ID="$(value userId)" -e VUS="${VUS:-10}" -e DURATION="${DURATION:-60s}" \
  grafana/k6:1.0.0 run - < "$here/cases-list.js"
