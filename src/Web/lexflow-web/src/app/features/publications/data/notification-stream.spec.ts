import { TestBed } from '@angular/core/testing';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { of } from 'rxjs';
import { APP_CONFIG } from '../../../core/config/app-config';
import { ActiveOrganization } from '../../../core/organization/active-organization';
import { NotificationStream } from './notification-stream';

const mock = vi.hoisted(() => ({
  url: '',
  options: {} as {
    accessTokenFactory: () => Promise<string>;
    headers: Record<string, string>;
    transport: number;
    withCredentials: boolean;
  },
  reconnected: () => undefined as unknown,
  closed: () => undefined as unknown,
  start: vi.fn(),
  stop: vi.fn(),
  subscribe: vi.fn(),
  dispose: vi.fn(),
}));
vi.mock('@microsoft/signalr', () => ({
  HttpTransportType: { LongPolling: 4 },
  LogLevel: { None: 6 },
  HubConnectionBuilder: class {
    withUrl(url: string, options: typeof mock.options) {
      mock.url = url;
      mock.options = options;
      return this;
    }
    configureLogging() {
      return this;
    }
    withAutomaticReconnect() {
      return this;
    }
    build() {
      return {
        start: mock.start,
        stop: mock.stop,
        stream: () => ({ subscribe: mock.subscribe }),
        onreconnecting: vi.fn(),
        onreconnected: (callback: () => void) => {
          mock.reconnected = callback;
        },
        onclose: (callback: () => void) => {
          mock.closed = callback;
        },
      };
    }
  },
}));

describe('NotificationStream', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.clearAllMocks();
    mock.start.mockResolvedValue(undefined);
    mock.stop.mockResolvedValue(undefined);
    mock.subscribe.mockReturnValue({ dispose: mock.dispose });
    TestBed.configureTestingModule({
      providers: [
        { provide: APP_CONFIG, useValue: { apiUrl: 'https://lexflow.test' } },
        { provide: ActiveOrganization, useValue: { id: () => 'office' } },
        { provide: OidcSecurityService, useValue: { getAccessToken: () => of('private-token') } },
      ],
    });
  });
  afterEach(() => vi.useRealTimers());
  it('uses LongPolling, headers and an access token factory without tokens in the URL', async () => {
    const dispose = TestBed.inject(NotificationStream).watch(vi.fn(), vi.fn());
    await Promise.resolve();
    expect(mock.url).toBe('https://lexflow.test/api/v1/notifications/hub');
    expect(mock.url).not.toContain('private-token');
    expect(mock.options.transport).toBe(4);
    expect(mock.options.withCredentials).toBe(false);
    expect(mock.options.headers['X-Organization-Id']).toBe('office');
    expect(await mock.options.accessTokenFactory()).toBe('private-token');
    dispose();
  });
  it('resubscribes after reconnection and disposes stream and connection', async () => {
    const dispose = TestBed.inject(NotificationStream).watch(vi.fn(), vi.fn());
    await Promise.resolve();
    expect(mock.subscribe).toHaveBeenCalledTimes(1);
    mock.reconnected();
    expect(mock.subscribe).toHaveBeenCalledTimes(2);
    dispose();
    expect(mock.dispose).toHaveBeenCalledTimes(2);
    expect(mock.stop).toHaveBeenCalled();
  });
  it('retries initial connection failure and cancels retry when stopped', async () => {
    mock.start.mockRejectedValueOnce(new Error('offline'));
    const dispose = TestBed.inject(NotificationStream).watch(vi.fn(), vi.fn());
    await vi.advanceTimersByTimeAsync(5000);
    expect(mock.start).toHaveBeenCalledTimes(2);
    mock.closed();
    dispose();
    await vi.advanceTimersByTimeAsync(60000);
    expect(mock.start).toHaveBeenCalledTimes(2);
  });
});
