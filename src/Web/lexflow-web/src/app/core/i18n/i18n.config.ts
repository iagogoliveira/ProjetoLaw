import {
  DOCUMENT,
  EnvironmentProviders,
  inject,
  Injectable,
  isDevMode,
  provideAppInitializer,
} from '@angular/core';
import {
  Translation,
  TranslocoLoader,
  TranslocoService,
  provideTransloco,
} from '@jsverse/transloco';

export const DEFAULT_LANG = 'pt-BR';
export const AVAILABLE_LANGS = ['pt-BR', 'en'] as const;

/**
 * Carrega as traduções como chunks lazy do bundle (import dinâmico),
 * sem HttpClient — HTTP fica restrito às camadas data/.
 */
@Injectable({ providedIn: 'root' })
export class BundledTranslationLoader implements TranslocoLoader {
  async getTranslation(lang: string): Promise<Translation> {
    const module = (await import(`./translations/${lang}.json`)) as { default: Translation };
    return module.default;
  }
}

export function provideI18n(): (EnvironmentProviders | ReturnType<typeof provideTransloco>)[] {
  return [
    provideTransloco({
      config: {
        availableLangs: [...AVAILABLE_LANGS],
        defaultLang: DEFAULT_LANG,
        fallbackLang: DEFAULT_LANG,
        reRenderOnLangChange: true,
        prodMode: !isDevMode(),
      },
      loader: BundledTranslationLoader,
    }),
    // Mantém <html lang> em sincronia com o idioma ativo (leitores de tela, hifenização).
    provideAppInitializer(() => {
      const transloco = inject(TranslocoService);
      const document = inject(DOCUMENT);
      transloco.langChanges$.subscribe((lang) =>
        document.documentElement.setAttribute('lang', lang),
      );
    }),
  ];
}
