import { EnvironmentProviders, Injectable, isDevMode, Provider } from '@angular/core';
import { provideTransloco, Translation, TranslocoLoader } from '@jsverse/transloco';

export const DEFAULT_LANG = 'pt-BR';
export const AVAILABLE_LANGS = ['pt-BR', 'en'] as const;

/**
 * Carrega as traduções por import dinâmico (empacotadas no build), sem HTTP:
 * chamadas HTTP só existem em `data/` (seção 5.2).
 */
@Injectable({ providedIn: 'root' })
export class BundledTranslocoLoader implements TranslocoLoader {
  getTranslation(lang: string): Promise<Translation> {
    switch (lang) {
      case 'en':
        return import('./en.json').then((m) => m.default);
      default:
        return import('./pt-BR.json').then((m) => m.default);
    }
  }
}

export function provideI18n(): (Provider | EnvironmentProviders)[] {
  return provideTransloco({
    config: {
      availableLangs: [...AVAILABLE_LANGS],
      defaultLang: DEFAULT_LANG,
      fallbackLang: DEFAULT_LANG,
      reRenderOnLangChange: true,
      prodMode: !isDevMode(),
      missingHandler: { useFallbackTranslation: true, logMissingKey: true },
    },
    loader: BundledTranslocoLoader,
  });
}
