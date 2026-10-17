import { AppConfig } from '../app-config';

/** Lê `/config.json` antes do bootstrap: o OIDC e o cliente da API dependem dos endereços. */
export async function loadAppConfig(fetchFn: typeof fetch = fetch): Promise<AppConfig> {
  const response = await fetchFn('/config.json', { cache: 'no-cache' });
  if (!response.ok) {
    throw new Error(`config.json indisponível (HTTP ${response.status}).`);
  }

  const config = (await response.json()) as Partial<AppConfig>;
  if (!config.apiUrl || !config.authority) {
    throw new Error('config.json precisa de apiUrl e authority.');
  }

  return {
    apiUrl: config.apiUrl.replace(/\/$/, ''),
    authority: config.authority.replace(/\/$/, ''),
  };
}
