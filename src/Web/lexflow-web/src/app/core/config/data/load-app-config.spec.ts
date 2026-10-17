import { loadAppConfig } from './load-app-config';

function respond(body: unknown, status = 200): typeof fetch {
  return vi
    .fn()
    .mockResolvedValue(new Response(JSON.stringify(body), { status })) as unknown as typeof fetch;
}

describe('loadAppConfig', () => {
  it('reads the addresses and drops trailing slashes', async () => {
    const config = await loadAppConfig(
      respond({ apiUrl: 'https://api.lexflow.test/', authority: 'https://id.lexflow.test/' }),
    );

    expect(config).toEqual({
      apiUrl: 'https://api.lexflow.test',
      authority: 'https://id.lexflow.test',
    });
  });

  it('fails when the file is missing or incomplete', async () => {
    await expect(loadAppConfig(respond({}, 404))).rejects.toThrow('HTTP 404');
    await expect(loadAppConfig(respond({ apiUrl: 'x' }))).rejects.toThrow('apiUrl e authority');
  });
});
