import { APIRequestContext, expect } from '@playwright/test';

interface MailpitSearch {
  messages: { ID: string }[];
}

/**
 * Espera o e-mail enviado pelo Worker chegar ao Mailpit e devolve o primeiro link do corpo que contém
 * `pathFragment` (com as entidades HTML já decodificadas).
 */
export async function linkFromEmail(
  request: APIRequestContext,
  mailpitUrl: string,
  to: string,
  pathFragment: string,
): Promise<string> {
  let html = '';
  await expect
    .poll(
      async () => {
        const search = await request.get(`${mailpitUrl}/api/v1/search`, {
          params: { query: `to:"${to}"` },
        });
        const { messages } = (await search.json()) as MailpitSearch;
        if (messages.length === 0) {
          return false;
        }

        const message = await request.get(`${mailpitUrl}/api/v1/message/${messages[0].ID}`);
        html = ((await message.json()) as { HTML: string }).HTML;
        return html.includes(pathFragment);
      },
      { timeout: 60_000, message: `e-mail para ${to} não chegou ao Mailpit` },
    )
    .toBe(true);

  const href = new RegExp(`href="([^"]*${pathFragment}[^"]*)"`).exec(html)?.[1];
  expect(href, 'link no e-mail').toBeTruthy();
  return href!.replaceAll('&amp;', '&');
}
