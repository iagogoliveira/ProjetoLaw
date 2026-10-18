-- Seed do teste de carga da lista de processos (Etapa 5, critério 1: 100 mil processos no tenant).
-- Variáveis (psql -v): org, user_id, area, other_area, client, total.
-- Roda como dono do banco (fora da RLS), num escritório criado só para a carga pelo prepare-load.spec.ts.
--   metade dos processos na área :area e metade em :other_area;
--   um em cada cinco com o usuário da carga como advogado responsável, os outros com responsáveis fictícios;
--   1% sigilosos (permitido só um usuário fictício), para o filtro de sigilo pesar na consulta.
BEGIN;

CREATE TEMP TABLE load_cases ON COMMIT DROP AS
SELECT gen_random_uuid() AS id,
       n,
       CASE WHEN n % 2 = 0 THEN :'area'::uuid ELSE :'other_area'::uuid END AS area,
       CASE WHEN n % 5 = 0 THEN :'user_id'::uuid ELSE gen_random_uuid() END AS responsible
FROM generate_series(1, :total) AS n;

INSERT INTO litigation.cases (id, organization_id, kind, internal_folder, status, practice_area_id, claim_value, custom_fields,
                              created_at, created_by, is_deleted)
SELECT id, :'org'::uuid, 'Extrajudicial', 'CARGA-' || lpad(n::text, 6, '0'), 'Active', area, (n % 1000) * 100,
       '{}'::jsonb, now() - (n || ' minutes')::interval, :'user_id'::uuid, false
FROM load_cases;

INSERT INTO litigation.case_parties (id, organization_id, legal_case_id, person_id, pole, is_client)
SELECT gen_random_uuid(), :'org'::uuid, id, :'client'::uuid, 'Active', true FROM load_cases;

INSERT INTO litigation.case_assignments (id, organization_id, legal_case_id, role, user_id)
SELECT gen_random_uuid(), :'org'::uuid, id, 'Lawyer', responsible FROM load_cases;

INSERT INTO litigation.case_restrictions (id, organization_id, legal_case_id, mode, user_id)
SELECT gen_random_uuid(), :'org'::uuid, id, 'Allow', gen_random_uuid() FROM load_cases WHERE n % 100 = 0;

COMMIT;

ANALYZE litigation.cases;
ANALYZE litigation.case_parties;
ANALYZE litigation.case_assignments;
ANALYZE litigation.case_restrictions;

SELECT count(*) AS seeded FROM litigation.cases WHERE organization_id = :'org'::uuid;
