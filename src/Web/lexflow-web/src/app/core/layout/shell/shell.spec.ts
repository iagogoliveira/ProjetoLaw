import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { translocoTesting } from '../../i18n/transloco-testing';
import { Shell } from './shell';

describe('Shell', () => {
  async function render() {
    await TestBed.configureTestingModule({
      imports: [Shell, translocoTesting()],
      providers: [provideRouter([])],
    }).compileComponents();

    const fixture = TestBed.createComponent(Shell);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('renders the application name in the toolbar', async () => {
    const element = await render();

    expect(element.querySelector('header .brand')?.textContent?.trim()).toBe('LexFlow');
  });

  it('labels the main navigation in pt-BR', async () => {
    const element = await render();

    expect(element.querySelector('nav')?.getAttribute('aria-label')).toBe('Navegação principal');
    expect(element.querySelector('nav')?.textContent).toContain('Nenhum módulo disponível ainda.');
  });

  it('offers a skip link to the main content', async () => {
    const element = await render();

    const skipLink = element.querySelector<HTMLAnchorElement>('a.skip-link');
    expect(skipLink?.textContent?.trim()).toBe('Pular para o conteúdo');
    expect(skipLink?.getAttribute('href')).toBe('#main-content');
    expect(element.querySelector('main#main-content')).not.toBeNull();
  });
});
