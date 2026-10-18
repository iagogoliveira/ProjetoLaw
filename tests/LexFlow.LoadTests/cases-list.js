// Teste de carga da lista de processos (Etapa 5, critério 1): com 100 mil processos no escritório, a lista filtrada
// por área e responsável responde em menos de 1,5 s no p95. Variáveis: API_URL, API_HOST, TOKEN, ORG_ID, AREA_ID, USER_ID.
import http from 'k6/http';
import { check } from 'k6';

export const options = {
  scenarios: {
    list: { executor: 'constant-vus', vus: Number(__ENV.VUS || 10), duration: __ENV.DURATION || '60s' },
  },
  thresholds: {
    'http_req_duration{filter:area_responsible}': ['p(95)<1500'],
    'http_req_duration{filter:area}': ['p(95)<1500'],
    http_req_failed: ['rate<0.01'],
  },
};

// API_HOST: o host original da API (o emissor do token é conferido pelo host da requisição; do container, a URL é
// host.docker.internal).
const headers = {
  Authorization: `Bearer ${__ENV.TOKEN}`,
  'X-Organization-Id': __ENV.ORG_ID,
  ...(__ENV.API_HOST ? { Host: __ENV.API_HOST } : {}),
};

export default function () {
  const both = http.get(`${__ENV.API_URL}/api/v1/cases?practiceAreaId=${__ENV.AREA_ID}&responsibleId=${__ENV.USER_ID}&limit=50`, {
    headers,
    tags: { filter: 'area_responsible' },
  });
  check(both, { 'área e responsável: 200 com 50 itens': (r) => r.status === 200 && r.json('items').length === 50 });

  const area = http.get(`${__ENV.API_URL}/api/v1/cases?practiceAreaId=${__ENV.AREA_ID}&limit=50`, { headers, tags: { filter: 'area' } });
  check(area, { 'área: 200 com 50 itens': (r) => r.status === 200 && r.json('items').length === 50 });
}
