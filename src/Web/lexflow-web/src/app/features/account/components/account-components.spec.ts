import { TestBed } from '@angular/core/testing';
import { translocoTesting } from '../../../core/i18n/transloco-testing';
import { MfaSetup, qrDataUrl } from './mfa-setup';
import { SessionsList } from './sessions-list';

describe('MfaSetup', () => {
  async function render(inputs: Record<string, unknown>) {
    await TestBed.configureTestingModule({
      imports: [MfaSetup, translocoTesting()],
    }).compileComponents();
    const fixture = TestBed.createComponent(MfaSetup);
    for (const [name, value] of Object.entries(inputs)) {
      fixture.componentRef.setInput(name, value);
    }
    await fixture.whenStable();
    return fixture;
  }

  it('offers both methods while mfa is off', async () => {
    const fixture = await render({ method: 'none' });
    const element = fixture.nativeElement as HTMLElement;

    const buttons = [...element.querySelectorAll('button')].map((b) => b.textContent?.trim());

    expect(buttons).toEqual(['Usar aplicativo autenticador', 'Receber código por e-mail']);
  });

  it('shows the qr code and the key, and emits the typed code', async () => {
    const fixture = await render({
      method: 'none',
      setup: {
        sharedKey: 'JBSWY3DPEHPK3PXP',
        authenticatorUri: 'otpauth://totp/LexFlow:ana?secret=JBSWY3DPEHPK3PXP',
      },
    });
    const element = fixture.nativeElement as HTMLElement;
    const emitted: string[] = [];
    fixture.componentInstance.confirmTotp.subscribe((code) => emitted.push(code));

    expect(element.querySelector('img.qr')?.getAttribute('src')).toMatch(
      /^data:image\/gif;base64,/,
    );
    expect(element.querySelector('[data-testid="totp-key"]')?.textContent).toBe('JBSWY3DPEHPK3PXP');
    const input = element.querySelector('input') as HTMLInputElement;
    input.value = '123456';
    input.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    element.querySelector('form')?.dispatchEvent(new Event('submit'));

    expect(emitted).toEqual(['123456']);
  });

  it('shows the recovery codes once totp is on', async () => {
    const fixture = await render({ method: 'totp', recoveryCodes: ['AAAAA-BBBBB', 'CCCCC-DDDDD'] });
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('[role="status"]')?.textContent).toContain(
      'aplicativo autenticador',
    );
    expect([...element.querySelectorAll('code')].map((c) => c.textContent)).toEqual([
      'AAAAA-BBBBB',
      'CCCCC-DDDDD',
    ]);
  });

  it('encodes text as a qr image', () => {
    expect(qrDataUrl('otpauth://totp/x')).toMatch(/^data:image\/gif;base64,/);
  });
});

describe('SessionsList', () => {
  const base = {
    createdAt: '2026-10-16T12:00:00Z',
    lastSeenAt: '2026-10-16T12:00:00Z',
    expiresAt: '2026-10-30T12:00:00Z',
  };

  it('marks the current session and lets the others be ended', async () => {
    await TestBed.configureTestingModule({
      imports: [SessionsList, translocoTesting()],
    }).compileComponents();
    const fixture = TestBed.createComponent(SessionsList);
    fixture.componentRef.setInput('sessions', [
      { ...base, id: 's1', ipAddress: null, userAgent: 'Firefox', current: true },
      { ...base, id: 's2', ipAddress: '203.0.113.7', userAgent: null, current: false },
    ]);
    const ended: string[] = [];
    fixture.componentInstance.endSession.subscribe((id) => ended.push(id));
    await fixture.whenStable();
    const items = (fixture.nativeElement as HTMLElement).querySelectorAll('li');

    expect(items[0].textContent).toContain('(esta sessão)');
    expect(items[0].querySelector('button')).toBeNull();
    expect(items[1].textContent).toContain('Dispositivo desconhecido');
    (items[1].querySelector('button') as HTMLButtonElement).click();
    expect(ended).toEqual(['s2']);
  });
});
