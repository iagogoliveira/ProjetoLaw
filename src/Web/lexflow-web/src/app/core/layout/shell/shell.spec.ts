import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import ptBR from '../../i18n/translations/pt-BR.json';
import { Shell } from './shell';

describe('Shell', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [
        Shell,
        TranslocoTestingModule.forRoot({
          langs: { 'pt-BR': ptBR },
          translocoConfig: { availableLangs: ['pt-BR'], defaultLang: 'pt-BR' },
          preloadLangs: true,
        }),
      ],
      providers: [provideRouter([])],
    }).compileComponents();
  });

  it('renderiza título traduzido em pt-BR', async () => {
    const fixture = TestBed.createComponent(Shell);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('[data-testid="brand"]')?.textContent?.trim()).toBe('LexFlow');
    expect(element.querySelector('nav')?.getAttribute('aria-label')).toBe('Navegação principal');
    expect(element.querySelector('.skip-link')?.textContent?.trim()).toBe('Pular para o conteúdo');
  });

  it('alterna o menu lateral', async () => {
    const fixture = TestBed.createComponent(Shell);
    await fixture.whenStable();
    const button = (fixture.nativeElement as HTMLElement).querySelector('button')!;

    expect(button.getAttribute('aria-expanded')).toBe('true');
    button.click();
    await fixture.whenStable();
    expect(button.getAttribute('aria-expanded')).toBe('false');
  });
});
