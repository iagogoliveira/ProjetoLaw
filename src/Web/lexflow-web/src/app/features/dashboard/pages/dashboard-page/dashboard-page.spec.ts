import { TestBed } from '@angular/core/testing';
import { translocoTesting } from '../../../../core/i18n/transloco-testing';
import { DashboardPage } from './dashboard-page';

describe('DashboardPage', () => {
  it('renders the empty home page in pt-BR', async () => {
    await TestBed.configureTestingModule({
      imports: [DashboardPage, translocoTesting()],
    }).compileComponents();

    const fixture = TestBed.createComponent(DashboardPage);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('h1')?.textContent?.trim()).toBe('Início');
    expect(element.textContent).toContain('Seu painel aparecerá aqui.');
  });
});
