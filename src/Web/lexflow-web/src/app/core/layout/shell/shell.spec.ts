import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthSession, SignedInUser } from '../../auth/auth-session';
import { translocoTesting } from '../../i18n/transloco-testing';
import { Shell } from './shell';

describe('Shell', () => {
  const signOut = vi.fn().mockResolvedValue(undefined);

  async function render() {
    const user = signal<SignedInUser | null>({ name: 'Ana Souza', email: 'ana@exemplo.com.br' });
    await TestBed.configureTestingModule({
      imports: [Shell, translocoTesting()],
      providers: [provideRouter([]), { provide: AuthSession, useValue: { user, signOut } }],
    }).compileComponents();

    const fixture = TestBed.createComponent(Shell);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('renders the application name and the signed-in user', async () => {
    const element = await render();

    expect(element.querySelector('header .brand')?.textContent?.trim()).toBe('LexFlow');
    expect(element.querySelector('header .user')?.textContent?.trim()).toBe('Ana Souza');
  });

  it('labels the main navigation in pt-BR with home and account links', async () => {
    const element = await render();

    const nav = element.querySelector('nav');
    expect(nav?.getAttribute('aria-label')).toBe('Navegação principal');
    expect([...(nav?.querySelectorAll('a') ?? [])].map((a) => a.textContent?.trim())).toEqual([
      'Início',
      'Minha conta',
    ]);
  });

  it('signs out from the toolbar', async () => {
    const element = await render();

    const button = [...element.querySelectorAll('header button')].find(
      (b) => b.textContent?.trim() === 'Sair',
    );
    (button as HTMLButtonElement).click();

    expect(signOut).toHaveBeenCalled();
  });

  it('offers a skip link to the main content', async () => {
    const element = await render();

    const skipLink = element.querySelector<HTMLAnchorElement>('a.skip-link');
    expect(skipLink?.textContent?.trim()).toBe('Pular para o conteúdo');
    expect(skipLink?.getAttribute('href')).toBe('#main-content');
    expect(element.querySelector('main#main-content')).not.toBeNull();
  });
});
