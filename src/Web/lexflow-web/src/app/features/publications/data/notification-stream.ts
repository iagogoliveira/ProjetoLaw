import { inject, Injectable } from '@angular/core';
import { HubConnectionBuilder, HttpTransportType, LogLevel } from '@microsoft/signalr';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { firstValueFrom } from 'rxjs';
import type { NotificationsPageResponse } from '../../../api/types.gen';
import { APP_CONFIG } from '../../../core/config/app-config';
import {
  ActiveOrganization,
  ORGANIZATION_HEADER,
} from '../../../core/organization/active-organization';

/** Transporte do hub existente; nenhuma credencial em URL, logs ou estado de tela. */
@Injectable({ providedIn: 'root' })
export class NotificationStream {
  private readonly oidc = inject(OidcSecurityService);
  private readonly config = inject(APP_CONFIG);
  private readonly active = inject(ActiveOrganization);

  watch(
    next: (page: NotificationsPageResponse) => void,
    status: (online: boolean) => void,
  ): () => void {
    const connection = new HubConnectionBuilder()
      .withUrl(`${this.config.apiUrl}/api/v1/notifications/hub`, {
        transport: HttpTransportType.LongPolling,
        withCredentials: false,
        accessTokenFactory: () => firstValueFrom(this.oidc.getAccessToken()),
        headers: this.active.id() ? { [ORGANIZATION_HEADER]: this.active.id()! } : {},
      })
      .configureLogging(LogLevel.None)
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (c) =>
          Math.min(30000, 1000 * 2 ** Math.min(c.previousRetryCount, 5)),
      })
      .build();
    let disposed = false;
    let retry: ReturnType<typeof setTimeout> | undefined;
    let subscription: { dispose(): void } | undefined;
    const subscribe = () => {
      subscription?.dispose();
      if (disposed) return;
      status(true);
      subscription = connection.stream<NotificationsPageResponse>('Watch').subscribe({
        next: (page) => {
          if (!disposed) next(page);
        },
        error: () => {
          status(false);
          if (!disposed) void connection.stop();
        },
        complete: () => {
          if (!disposed) void connection.stop();
        },
      });
    };
    const start = async () => {
      try {
        await connection.start();
        if (disposed) {
          await connection.stop();
          return;
        }
        subscribe();
      } catch {
        status(false);
        if (!disposed) retry = setTimeout(() => void start(), 5000);
      }
    };
    connection.onreconnecting(() => status(false));
    connection.onreconnected(subscribe);
    connection.onclose(() => {
      status(false);
      if (!disposed) retry = setTimeout(() => void start(), 5000);
    });
    void start();
    return () => {
      disposed = true;
      clearTimeout(retry);
      subscription?.dispose();
      void connection.stop();
    };
  }
}
