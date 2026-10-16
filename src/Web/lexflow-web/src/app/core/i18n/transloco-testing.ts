import { TranslocoTestingModule, TranslocoTestingOptions } from '@jsverse/transloco';
import en from './en.json';
import ptBR from './pt-BR.json';
import { DEFAULT_LANG } from './transloco.config';

/** Transloco com as traduções reais, carregadas de forma síncrona, para testes de componente. */
export function translocoTesting(options: TranslocoTestingOptions = {}) {
  return TranslocoTestingModule.forRoot({
    langs: { 'pt-BR': ptBR, en },
    translocoConfig: { availableLangs: ['pt-BR', 'en'], defaultLang: DEFAULT_LANG },
    preloadLangs: true,
    ...options,
  });
}
